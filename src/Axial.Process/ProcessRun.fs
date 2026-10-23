namespace Axial.Process

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService

type private StopCause =
    | Interrupted
    | StageTimeout of stage: int * after: TimeSpan
    | OverallTimeout of after: TimeSpan
    | DrainTimeout of after: TimeSpan
    | WorkerFailure

type private RunStage = {
    Process: Diagnostics.Process
    Pidfd: int
    Released: Deferred<Never, unit>
    Exit: Deferred<Never, int>
    mutable Exited: bool
    mutable StdinClosed: bool
    mutable Feeder: Fiber<ProcessError, unit> option
}

type private RunCommand =
    | RegisterFeeder of stage: int * feeder: Fiber<ProcessError, unit>
    | Close of stage: int * request: StopRequest * reply: Deferred<StopRequestError, unit>
    | Exited of stage: int * code: int
    | Stop of cause: StopCause * reply: Deferred<Never, StopCause>

/// Flow-based process execution with scoped processes, supervised pumps, and one stop coordinator.
module internal ProcessRun =
    let isEnabled () =
        Environment.GetEnvironmentVariable("AXIAL_PROCESS_ENGINE") = "flow" // axial-allow-effect: environment

    let private processError (command: CommandDefinition) (error: exn) =
        ProcessError.StartFailed {
            Command = Configuration.renderCommand command
            Message = error.Message
        }

    let private acquireStage redirectInput redirectOutput redirectError (command: CommandDefinition) =
        Flow.delay (fun () ->
            let created =
                try
                    Ok(new Diagnostics.Process(
                        StartInfo = NativeProcess.startInfo redirectInput redirectOutput redirectError command
                    ))
                with error ->
                    Error error

            match created with
            | Error error -> Flow.fail (processError command error)
            | Ok proc ->
                try
                    // OS bridge: Process.Start is the process service's named operational boundary.
                    // axial-allow-effect: process
                    if proc.Start() then
                        Flow.succeed proc
                    else
                        proc.Dispose()
                        Flow.fail (processError command (InvalidOperationException "Process.Start returned false."))
                with error ->
                    proc.Dispose()
                    Flow.fail (processError command error))

    let private releaseStage (proc: Diagnostics.Process) : Flow<ClockEnvironment, Never, unit> =
        Flow.delay (fun () ->
            try
                if not proc.HasExited then
#if NETSTANDARD2_1
                    proc.Kill()
#else
                    // OS bridge: abnormal scope close must terminate the owned process tree.
                    proc.Kill(entireProcessTree = true)
#endif
            with _ ->
                ()

            try
                if proc.StartInfo.RedirectStandardInput then proc.StandardInput.BaseStream.Dispose()
                if proc.StartInfo.RedirectStandardOutput then proc.StandardOutput.BaseStream.Dispose()
                if proc.StartInfo.RedirectStandardError then proc.StandardError.BaseStream.Dispose()
            with _ ->
                ()

            proc.Dispose()
            Flow.succeed ())

    let private waitForStage (proc: Diagnostics.Process) =
        // Task bridge: WaitForExitAsync is the OS completion notification.
        Flow.fromTask (fun token -> task {
            do! NativeProcess.waitForExit proc token
            return proc.ExitCode
        })

    let private readChunk (stream: Stream) (buffer: byte array) =
        // Task bridge: one asynchronous pipe read; looping stays in Flow.
        Flow.fromTask (fun token -> stream.ReadAsync(buffer, 0, buffer.Length, token))

    let private closeInput (proc: Diagnostics.Process) =
        Flow.delay (fun () ->
            try proc.StandardInput.Close() with _ -> ()
            Flow.succeed ())

    let rec private hasTypedFailure cause =
        match cause with
        | Cause.Fail _ -> true
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> hasTypedFailure left || hasTypedFailure right
        | Cause.Traced(inner, _) -> hasTypedFailure inner
        | _ -> false

    let private mapInputFailure (source: Flow<'env, string, unit>) =
        source
        |> Flow.foldCause
            Flow.succeed
            (fun cause ->
                if hasTypedFailure cause then
                    Flow.fail (ProcessError.InputFailed(cause.ToString()))
                else
                    Flow.ofExit (Exit.Failure(Cause.map ProcessError.InputFailed cause)))

    let private mapEngineFailure (source: Flow<ClockEnvironment, ProcessError, 'value>) =
        source
        |> Flow.foldCause
            Flow.succeed
            (fun cause ->
                match Cause.untraced cause with
                | Cause.Die (:? ProcessFailureException as failure) -> Flow.fail failure.Error
                | Cause.Die (:? ObserverDefectException as defect) -> Flow.die defect.InnerException
                | _ -> Flow.ofExit (Exit.Failure cause))

    let execute
        (clock: IClock)
        (fileSystem: IFileSystem)
        (console: IConsole)
        (specification: ProcessSpec)
        (observe: ProcessOutput -> Async<unit>)
        : Flow<unit, ProcessError, ProcessResult> =
        let clockEnvironment = ClockEnvironment clock

        let run : Flow<ClockEnvironment, ProcessError, ProcessResult> =
            flow {
                let commands = specification.Commands |> List.toArray
                let stdoutLimit, stderrLimit = specification.MaxOutputBytes

                for command in commands do
                    if stdoutLimit.IsSome && NativeProcess.inherited command.StdOut.Value then
                        // axial-allow-raise
                        invalidArg "specification" "A stdout byte cap cannot be enforced on inherited stdout."
                    if stderrLimit.IsSome && NativeProcess.inherited command.StdErr.Value then
                        // axial-allow-raise
                        invalidArg "specification" "A stderr byte cap cannot be enforced on inherited stderr."

                let stdoutTargets, stderrTargets, stdout, stderr =
                    NativeProcess.prepareTargetsAndCaptures specification commands

                let files = Dictionary<string * bool, FileStream>()

                let! () =
                    Flow.scopeFinalizer (
                        Flow.delay (fun () ->
                            for file in files.Values do file.Dispose()
                            for buffer in stdout do (buffer :> IDisposable).Dispose()
                            for buffer in stderr do (buffer :> IDisposable).Dispose()
                            Flow.succeed ())
                    )

                do!
                    Flow.delay (fun () ->
                        try
                            Array.iter (NativeProcess.openTargets fileSystem files) stdoutTargets
                            Array.iter (NativeProcess.openTargets fileSystem files) stderrTargets
                            Flow.succeed ()
                        with error ->
                            Flow.fail (ProcessError.IoFailed { Message = error.Message }))

                let startedAt = clock.UtcNow()
                let startedElapsed = clock.Elapsed()
                let starts = ResizeArray<DateTimeOffset>()
                let elapsedStarts = ResizeArray<TimeSpan>()
                let elapsedEnds = Array.create commands.Length None
                let stdinOwned = Array.create commands.Length false
                let processes = ResizeArray<Diagnostics.Process>()
                let outputGates = Array.init commands.Length (fun _ -> new SemaphoreSlim(1, 1))
                let errorGates = Array.init commands.Length (fun _ -> new SemaphoreSlim(1, 1))
                let observerGate = new SemaphoreSlim(1, 1)
                let byteCounts = Array.init commands.Length (fun _ -> Array.zeroCreate<int64> 2)
                let countBytes = NativeProcess.countBytes specification commands byteCounts

                let! () =
                    Flow.scopeFinalizer (
                        Flow.delay (fun () ->
                            observerGate.Dispose()
                            for gate in outputGates do gate.Dispose()
                            for gate in errorGates do gate.Dispose()
                            Flow.succeed ())
                    )

                let! released =
                    commands
                    |> Flow.traverse (fun _ -> Deferred.make<ClockEnvironment, Never, unit> ())
                    |> Flow.widenError
                let! exits =
                    commands
                    |> Flow.traverse (fun _ -> Deferred.make<ClockEnvironment, Never, int> ())
                    |> Flow.widenError
                let! (commandsQueue: Queue<RunCommand>) = Queue.unbounded ()
                let timerFibers = ResizeArray<Fiber<ProcessError, unit>>()

                let postTimedStop cause error =
                    flow {
                        let! reply = Deferred.make<ClockEnvironment, Never, StopCause> () |> Flow.widenError
                        let! _ = commandsQueue |> Queue.offer (Stop(cause, reply))
                        let! winner = Deferred.await reply |> Flow.widenError
                        if winner = cause then return! Flow.fail error
                        else return! Flow.never<ClockEnvironment, ProcessError, unit>
                    }

                let sleepStartedNow delay =
                    let cancellation = new CancellationTokenSource()
                    let pending = clock.Sleep(delay, cancellation.Token)
                    // The started sleep owns a CTS canceled by the Flow finalizer.
                    Flow.fromTask (fun _ -> task { do! pending }) // axial-allow-discarded-cancellation
                    |> Flow.ensuring (
                        Flow.delay (fun () ->
                            try cancellation.Cancel() with _ -> ()
                            cancellation.Dispose()
                            Flow.succeed ())
                    )

                let startTimer cause error delay =
                    flow {
                        do! sleepStartedNow delay
                        return! postTimedStop cause error
                    }

                match specification.OverallTimeout with
                | Some after ->
                    let remaining = max TimeSpan.Zero (after - (clock.Elapsed() - startedElapsed))
                    let error =
                        ProcessError.OverallTimedOut {
                            Specification = commands |> Array.map Configuration.renderCommand |> String.concat " | "
                            Timeout = after
                        }
                    let! timer = startTimer (OverallTimeout after) error remaining |> Flow.forkNamed "process-overall-timeout"
                    timerFibers.Add timer
                | None -> ()

                for stage, command in commands |> Array.indexed do
                    let pipedOut = specification.Connections |> List.exists (fun (source, _, _) -> source = stage)
                    let pipedError = specification.Connections |> List.exists (fun (source, _, both) -> source = stage && both)
                    let isRoot = specification.Connections |> List.forall (fun (_, target, _) -> target <> stage)
                    let merged = specification.MergeStdErr && specification.Leaves.Contains stage
                    let redirectInput =
                        not (isRoot && (match command.StdIn.Value with InputSource.Inherit -> true | _ -> false))
                    let redirectOutput = pipedOut || not (NativeProcess.inherited stdoutTargets[stage])
                    let redirectError =
                        pipedError
                        || not (NativeProcess.inherited (if merged then stdoutTargets[stage] else stderrTargets[stage]))

                    let! proc =
                        Flow.scopeAcquireRelease
                            (acquireStage redirectInput redirectOutput redirectError command)
                            releaseStage

                    processes.Add proc
                    stdinOwned[stage] <- redirectInput
                    let stageStartedAt = clock.UtcNow()
                    starts.Add stageStartedAt
                    elapsedStarts.Add(clock.Elapsed())

                    match command.Timeout.Value with
                    | Some after ->
                        let timeout =
                            Flow.race
                                (Deferred.await exits[stage] |> Flow.widenError |> Flow.map (fun _ -> false))
                                (sleepStartedNow after |> Flow.map (fun () -> true))
                            |> Flow.bind (fun timedOut ->
                                if timedOut then
                                    postTimedStop
                                        (StageTimeout(stage, after))
                                        (ProcessError.StageTimedOut {
                                            Stage = stage
                                            Command = Configuration.renderCommand command
                                            Timeout = after
                                        })
                                else
                                    Flow.never<ClockEnvironment, ProcessError, unit>)
                        let! timer = timeout |> Flow.forkNamed ("process-stage-timeout-" + string stage)
                        timerFibers.Add timer
                    | None -> ()

                let stages =
                    processes
                    |> Seq.mapi (fun stage proc -> {
                        Process = proc
                        Pidfd = NativeProcess.tryOpenPidfd proc.Id
                        Released = released[stage]
                        Exit = exits[stage]
                        Exited = false
                        StdinClosed = false
                        Feeder = None
                    })
                    |> Seq.toArray

                let! () =
                    Flow.scopeFinalizer (
                        Flow.delay (fun () ->
                            for stage in stages do NativeProcess.closePidfd stage.Pidfd
                            Flow.succeed ())
                    )

                let closePipe (stage: RunStage) =
                    Flow.delay (fun () ->
                        stage.StdinClosed <- true
                        try stage.Process.StandardInput.BaseStream.Dispose() with _ -> ()
                        try stage.Process.StandardInput.Close() with _ -> ()
                        Deferred.succeedNow () stage.Released |> ignore
                        Flow.succeed ())

                let stopFeeder (stage: RunStage) =
                    flow {
                        match stage.Feeder with
                        | Some feeder ->
                            let! settled = Fiber.poll feeder
                            match settled with
                            | Some _ -> ()
                            | None ->
                                do! Fiber.requestInterrupt feeder
                                do!
                                    Flow.race
                                        (Fiber.await feeder |> Flow.ignore)
                                        (Flow.sleep (TimeSpan.FromMilliseconds 250.0))
                        | None -> ()
                        do! closePipe stage
                    }

                let completeClose result reply =
                    match result with
                    | Ok () -> Deferred.succeed () reply |> Flow.ignore
                    | Error error -> Deferred.fail error reply |> Flow.ignore

                let isLive (stage: RunStage) =
                    try not stage.Process.HasExited with _ -> false

                let terminateAll () =
                    flow {
                        do!
                            Flow.delay (fun () ->
                                for stage in stages do
                                    try
#if NETSTANDARD2_1
                                        stage.Process.Kill()
#else
                                        // A failed pump may race the direct child's exit while a descendant still
                                        // owns its pipes, so abnormal termination always attempts the whole tree.
                                        stage.Process.Kill(entireProcessTree = true)
#endif
                                    with _ -> ()
                                    try
                                        if stage.Process.StartInfo.RedirectStandardOutput then stage.Process.StandardOutput.BaseStream.Dispose()
                                        if stage.Process.StartInfo.RedirectStandardError then stage.Process.StandardError.BaseStream.Dispose()
                                    with _ -> ()
                                Flow.succeed ())
                        for stage in stages do
                            if stage.Feeder.IsSome && not stage.StdinClosed then do! stopFeeder stage
                    }

                let sendRequest request (stage: RunStage) =
                    flow {
                        if request = StopRequest.CloseStdIn then
                            if stage.Process.StartInfo.RedirectStandardInput then
                                do! stopFeeder stage
                                return true
                            else
                                return false
                        else
                            return NativeProcess.sendStopRequest stage.Process stage.Pidfd request = Ok ()
                    }

                let runStopLadder () =
                    flow {
                        for request, grace in specification.StopGracefully do
                            let live = stages |> Array.filter isLive
                            let! sent =
                                live
                                |> Array.map (sendRequest request)
                                |> Array.toList
                                |> Flow.sequencePar
                            if sent |> List.exists id && live.Length > 0 && grace > TimeSpan.Zero then
                                let exited =
                                    live
                                    |> Array.map (fun stage -> Deferred.await stage.Exit)
                                    |> Array.toList
                                    |> Flow.sequencePar
                                    |> Flow.ignore
                                do! Flow.race exited (Flow.sleep grace)
                        do! terminateAll ()
                    }

                let mutable firstStop: StopCause option = None
                let! stopFinished =
                    Deferred.make<ClockEnvironment, Never, StopCause> ()
                    |> Flow.widenError
                let! ioStop =
                    Deferred.make<ClockEnvironment, Never, unit> ()
                    |> Flow.widenError

                let stop cause reply =
                    flow {
                        match firstStop with
                        | Some winner ->
                            let! _ = Deferred.succeed winner reply
                            ()
                        | None ->
                            firstStop <- Some cause
                            Deferred.succeedNow () ioStop |> ignore
                            do! runStopLadder ()
                            let! _ = Deferred.succeed cause stopFinished
                            let! _ = Deferred.succeed cause reply
                            ()
                    }

                let rec coordinate () =
                    flow {
                        let! command = Dequeue.take commandsQueue
                        match command with
                        | RegisterFeeder(stage, feeder) ->
                            let state = stages[stage]
                            state.Feeder <- Some feeder
                            if state.StdinClosed || state.Exited then do! stopFeeder state
                        | Exited(stage, code) ->
                            let state = stages[stage]
                            state.Exited <- true
                            if state.Feeder.IsSome && not state.StdinClosed then do! stopFeeder state
                            if not (commands[stage].SuccessCodes.Value.Contains code) then
                                let! ignored = Deferred.make<ClockEnvironment, Never, StopCause> ()
                                do! stop WorkerFailure ignored
                        | Close(stage, request, reply) ->
                            let state = stages[stage]
                            if state.Exited then
                                do! completeClose (Ok ()) reply
                            elif request = StopRequest.CloseStdIn then
                                if stdinOwned[stage] then
                                    do! stopFeeder state
                                    do! completeClose (Ok ()) reply
                                else
                                    do! completeClose (Error StopRequestError.Unsupported) reply
                            else
                                let result = NativeProcess.sendStopRequest state.Process state.Pidfd request
                                do! completeClose result reply
                        | Stop(cause, reply) -> do! stop cause reply
                        return! coordinate ()
                    }

                // The coordinator must outlive caller cancellation long enough for Flow.onInterrupt to post and
                // await the stop ladder. Scope close shuts its queue down after the handler has finished.
                let! coordinator =
                    coordinate ()
                    |> Flow.forkGraceful (Dequeue.shutdown commandsQueue) (TimeSpan.FromSeconds 5.0)

                let requestStop cause =
                    flow {
                        let! reply = Deferred.make<ClockEnvironment, Never, StopCause> ()
                        let! offered =
                            (commandsQueue |> Queue.offer (Stop(cause, reply)) |> Flow.map Some)
                            |> Flow.foldCause Flow.succeed (fun _ -> Flow.succeed None)
                        match offered with
                        | Some _ -> return! Deferred.await reply
                        | None -> return cause
                    }

                let closeCallback stage request = async {
                    let requestFlow =
                        flow {
                            let! reply = Deferred.make<ClockEnvironment, StopRequestError, unit> ()
                            let! _ = commandsQueue |> Queue.offer (Close(stage, request, reply))
                            return!
                                Deferred.await reply
                                |> Flow.map (fun () -> Ok ())
                                |> Flow.recover (Error >> Flow.succeed)
                        }
                        |> Flow.foldCause Flow.succeed (fun _ -> Flow.succeed (Ok ()))

                    let! exit = Flow.toAsync clockEnvironment requestFlow
                    return
                        match exit with
                        | Exit.Success result -> result
                        | Exit.Failure _ -> Ok ()
                }

                match specification.OnStarted with
                | Some handler ->
                    for stage, command in commands |> Array.indexed do
                        let! handlerExit =
                            handler.Invoke(
                                stage,
                                processes[stage].Id,
                                Configuration.renderCommand command,
                                starts[stage],
                                closeCallback stage
                            )
                            |> Flow.fromAsync
                            |> Flow.onInterrupt (requestStop Interrupted |> Flow.ignore)
                        do! Flow.ofExit handlerExit |> Flow.widenError
                | None -> ()

                let monitor stage =
                    waitForStage processes[stage]
                    |> Flow.tap (fun code ->
                        flow {
                            elapsedEnds[stage] <- Some(clock.Elapsed())
                            Deferred.succeedNow code exits[stage] |> ignore
                            let! _ = commandsQueue |> Queue.offer (Exited(stage, code))
                            return ()
                        })

                let publish stage original observed text =
                    Flow.fromTask (fun token ->
                        NativeProcess.publishObservation clock observe observerGate token stage original observed text)

                let outputPump stage original observed target (capture: CaptureBuffer) (stream: Stream) =
                    let buffer = Array.zeroCreate<byte> 4096
                    let decoder = commands[stage].Encoding.Value.GetDecoder()
                    let chars = Array.zeroCreate<char>(commands[stage].Encoding.Value.GetMaxCharCount buffer.Length)
                    let pending = StringBuilder()
                    let gate = if observed = OutputChannel.StdOut then outputGates[stage] else errorGates[stage]

                    let decode bytes count flush =
                        flow {
                            let length = decoder.GetChars(bytes, 0, count, chars, 0, flush)
                            match specification.Framing with
                            | OutputFraming.Chunks ->
                                if length > 0 then do! publish stage original observed (String(chars, 0, length))
                            | OutputFraming.Lines ->
                                for index in 0 .. length - 1 do
                                    let value = chars[index]
                                    if value = '\n' then
                                        if pending.Length > 0 && pending[pending.Length - 1] = '\r' then
                                            pending.Length <- pending.Length - 1
                                        do! publish stage original observed (pending.ToString())
                                        pending.Clear() |> ignore
                                    else
                                        pending.Append value |> ignore
                                        if pending.Length > specification.MaxLineLength
                                           && not (pending.Length = specification.MaxLineLength + 1 && value = '\r') then
                                            return! Flow.fail (ProcessError.LineTooLong {
                                                Stage = stage
                                                OriginalChannel = original
                                                Limit = specification.MaxLineLength
                                            })
                                if flush && pending.Length > 0 then
                                    if pending.Length > specification.MaxLineLength then
                                        return! Flow.fail (ProcessError.LineTooLong {
                                            Stage = stage
                                            OriginalChannel = original
                                            Limit = specification.MaxLineLength
                                        })
                                    do! publish stage original observed (pending.ToString())
                                    pending.Clear() |> ignore
                        }

                    let rec loop () =
                        flow {
                            let! count = readChunk stream buffer
                            if count = 0 then
                                do! decode [||] 0 true
                            else
                                countBytes stage original count
                                // Full reads can use the stable pump buffer directly. Avoiding one 4 KiB copy per
                                // read materially reduces allocation for large captures.
                                let bytes = if count = buffer.Length then buffer else buffer[.. count - 1]
                                do!
                                    Flow.fromTask (fun token ->
                                        NativeProcess.writeCapturedTarget console files token gate observed target capture bytes)
                                do! decode bytes count false
                                return! loop ()
                        }
                    loop ()

                let pipelinePump target (connections: (int * int * bool) list) =
                    let gate = new SemaphoreSlim(1, 1)
                    let write bytes =
                        Flow.fromTask (fun token -> task {
                            do! gate.WaitAsync token
                            try
                                do! processes[target].StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, token)
                            finally
                                gate.Release() |> ignore
                        })

                    let copy source channel (stream: Stream) =
                        let buffer = Array.zeroCreate<byte> 4096
                        let pending = new MemoryStream()
                        let lineMode = connections.Length > 1 && specification.Framing = OutputFraming.Lines
                        let decoder = commands[source].Encoding.Value.GetDecoder()
                        let decoded = Array.zeroCreate<char>(commands[source].Encoding.Value.GetMaxCharCount 1)
                        let singleByte = Array.zeroCreate<byte> 1
                        let mutable lineLength = 0
                        let mutable lastWasCR = false

                        let checkLength () =
                            if lineLength > specification.MaxLineLength
                               && not (lineLength = specification.MaxLineLength + 1 && lastWasCR) then
                                Error (ProcessError.LineTooLong {
                                    Stage = source
                                    OriginalChannel = channel
                                    Limit = specification.MaxLineLength
                                })
                            else
                                Ok ()

                        let processChunk count =
                            flow {
                                if lineMode then
                                    for index in 0 .. count - 1 do
                                        let value = buffer[index]
                                        pending.WriteByte value
                                        singleByte[0] <- value
                                        let length = decoder.GetChars(singleByte, 0, 1, decoded, 0, false)
                                        for charIndex in 0 .. length - 1 do
                                            if decoded[charIndex] = '\n' then
                                                do! write (pending.ToArray())
                                                pending.SetLength 0L
                                                lineLength <- 0
                                                lastWasCR <- false
                                            else
                                                lineLength <- lineLength + 1
                                                lastWasCR <- decoded[charIndex] = '\r'
                                                match checkLength () with
                                                | Ok () -> ()
                                                | Error error -> return! Flow.fail error
                                elif count > 0 then
                                    do! write (if count = buffer.Length then buffer else buffer[.. count - 1])
                            }

                        let rec loop () =
                            flow {
                                let! count = readChunk stream buffer
                                if count = 0 then
                                    if lineMode then
                                        lineLength <- lineLength + decoder.GetChars([||], 0, 0, decoded, 0, true)
                                        match checkLength () with
                                        | Error error -> return! Flow.fail error
                                        | Ok () when pending.Length > 0L -> do! write (pending.ToArray())
                                        | Ok () -> ()
                                else
                                    countBytes source channel count
                                    do! processChunk count
                                    return! loop ()
                            }

                        loop ()
                        |> Flow.ensuring (Flow.delay (fun () -> pending.Dispose(); Flow.succeed ()))

                    flow {
                        do!
                            connections
                            |> List.collect (fun (source, _, both) ->
                                [ yield copy source OutputChannel.StdOut processes[source].StandardOutput.BaseStream
                                  if both then
                                      yield copy source OutputChannel.StdErr processes[source].StandardError.BaseStream ])
                            |> Flow.sequencePar
                            |> Flow.ignore
                    }
                    |> Flow.foldCause
                        Flow.succeed
                        (fun cause ->
                            match Cause.untraced cause with
                            | Cause.Die (:? IOException as error) ->
                                Flow.fail (ProcessError.IoFailed { Message = error.Message })
                            | _ -> Flow.ofExit (Exit.Failure cause))
                    |> Flow.ensuring (
                        flow {
                            gate.Dispose()
                            do! closeInput processes[target]
                        }
                        |> Flow.mapError Never.absurd
                    )

                let copies =
                    specification.Connections
                    |> List.groupBy (fun (_, target, _) -> target)
                    |> List.map (fun (target, connections) -> pipelinePump target connections)

                let reads = ResizeArray<Flow<ClockEnvironment, ProcessError, unit>>()
                let final = Set.minElement specification.Leaves

                if not (NativeProcess.inherited stdoutTargets[final]) then
                    reads.Add(outputPump final OutputChannel.StdOut OutputChannel.StdOut stdoutTargets[final] stdout[final] processes[final].StandardOutput.BaseStream)

                for stage in 0 .. commands.Length - 1 do
                    if not (specification.Connections |> List.exists (fun (source, _, both) -> source = stage && both)) then
                        let merged = specification.MergeStdErr && stage = final
                        let target = if merged then stdoutTargets[stage] else stderrTargets[stage]
                        if not (NativeProcess.inherited target) then
                            reads.Add(
                                outputPump
                                    stage
                                    OutputChannel.StdErr
                                    (if merged then OutputChannel.StdOut else OutputChannel.StdErr)
                                    target
                                    (if merged then stdout[stage] else stderr[stage])
                                    processes[stage].StandardError.BaseStream)

                let writeInput stage bytes =
                    Flow.fromTask (fun token -> task {
                        do! processes[stage].StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, token)
                        return ()
                    })

                let inputFeeder stage source =
                    let pumping =
                        match source with
                        | InputSource.Empty -> Flow.succeed ()
                        | InputSource.Inherit -> Flow.succeed ()
                        | InputSource.Text text -> writeInput stage (commands[stage].Encoding.Value.GetBytes text)
                        | InputSource.Bytes bytes -> writeInput stage bytes
                        | InputSource.File path ->
                            Flow.fromTask (fun token -> task {
                                use input = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                                do! input.CopyToAsync(processes[stage].StandardInput.BaseStream, 81920, token)
                            })
                        | InputSource.Stream source ->
                            let work =
                                source
                                |> FlowStream.runForEachFlow (writeInput stage >> Flow.localEnv (fun () -> clockEnvironment))
                                |> mapInputFailure
                                |> Flow.localEnv (fun (_: ClockEnvironment) -> ())

                            // A foreign source may ignore interruption. Give it 250 ms to settle, then let the
                            // feeder fiber finish while a continuation observes and disposes the detached run.
                            Flow.fromTask (fun token -> task {
                                let sourceCancellation = new CancellationTokenSource()
                                let pumping =
                                    Async.StartAsTask(
                                        Flow.toAsync clockEnvironment work,
                                        cancellationToken = sourceCancellation.Token
                                    )
                                let canceled = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                                use _registration = token.Register(fun () -> canceled.TrySetResult() |> ignore)
                                let! first = Task.WhenAny(pumping :> Task, canceled.Task)
                                if obj.ReferenceEquals(first, pumping) then
                                    let! outcome =
                                        if pumping.IsCanceled then
                                            Task.FromResult(Exit.Failure Cause.Interrupt)
                                        else
                                            pumping
                                    sourceCancellation.Dispose()
                                    return outcome
                                else
                                    ThreadPool.QueueUserWorkItem(fun _ ->
                                        try sourceCancellation.Cancel() with _ -> ())
                                    |> ignore
                                    let timer = clock.Sleep(TimeSpan.FromMilliseconds 250.0, CancellationToken.None)
                                    let! settled = Task.WhenAny(pumping :> Task, timer)
                                    if obj.ReferenceEquals(settled, pumping) then
                                        let! outcome =
                                            if pumping.IsCanceled then
                                                Task.FromResult(Exit.Failure Cause.Interrupt)
                                            else
                                                pumping
                                        sourceCancellation.Dispose()
                                        return outcome
                                    else
                                        pumping.ContinueWith(
                                            (fun (_: Task<Exit<unit, ProcessError>>) ->
                                                sourceCancellation.Dispose()),
                                            CancellationToken.None,
                                            TaskContinuationOptions.ExecuteSynchronously,
                                            TaskScheduler.Default
                                        )
                                        |> ignore
                                        return Exit.Failure Cause.Interrupt
                            })
                            |> Flow.bind Flow.ofExit

                    pumping
                    |> Flow.foldCause
                        Flow.succeed
                        (fun cause ->
                            let expectedPipeClose =
                                match Cause.untraced cause with
                                | Cause.Interrupt -> stages[stage].StdinClosed
                                | Cause.Die error ->
                                    stages[stage].StdinClosed
                                    && (error :? ObjectDisposedException
                                        || error :? IOException
                                        || error :? OperationCanceledException)
                                | _ -> false
                            if expectedPipeClose then Flow.succeed ()
                            else Flow.ofExit (Exit.Failure cause))
                    |> Flow.ensuring (
                        flow {
                            stages[stage].StdinClosed <- true
                            do! closeInput processes[stage]
                        }
                        |> Flow.mapError Never.absurd
                    )

                let inputFibers = ResizeArray<Fiber<ProcessError, unit> * Deferred<Never, unit>>()

                for stage, command in commands |> Array.indexed do
                    if specification.Connections |> List.forall (fun (_, target, _) -> target <> stage) then
                        match command.StdIn.Value with
                        | InputSource.Inherit -> ()
                        | InputSource.Empty ->
                            stages[stage].StdinClosed <- true
                            do! closeInput processes[stage]
                        | source ->
                            let! feeder = inputFeeder stage source |> Flow.forkNamed ("process-stdin-" + string stage)
                            inputFibers.Add(feeder, released[stage])
                            let! _ = commandsQueue |> Queue.offer (RegisterFeeder(stage, feeder))
                            ()

                let awaitInput (feeder, detached: Deferred<Never, unit>) =
                    Flow.race
                        (Fiber.await feeder
                         |> Flow.bind (function
                             | Exit.Success () -> Flow.succeed ()
                             | Exit.Failure cause when Cause.isInterrupted cause -> Flow.succeed ()
                             | Exit.Failure cause -> Flow.ofExit (Exit.Failure cause)))
                        (Deferred.await detached |> Flow.widenError)

                let tolerateStopPipeFailure source =
                    source
                    |> Flow.foldCause
                        Flow.succeed
                        (fun cause ->
                            let expected =
                                match firstStop, Cause.untraced cause with
                                | Some _, Cause.Die error ->
                                    error :? IOException
                                    || error :? ObjectDisposedException
                                    || error :? OperationCanceledException
                                | _ -> false
                            if expected then Flow.succeed ()
                            else Flow.ofExit (Exit.Failure cause))

                let stopIo source =
                    Flow.race source (Deferred.await ioStop |> Flow.widenError)

                let stopWorkerFailure =
                    function
                    | Exit.Failure cause when not (Cause.isInterrupted cause) ->
                        requestStop WorkerFailure |> Flow.ignore
                    | _ -> Flow.succeed ()

                let! pumpFibers =
                    copies @ List.ofSeq reads
                    |> Flow.traverse (tolerateStopPipeFailure >> stopIo >> Flow.onExit stopWorkerFailure >> Flow.fork)

                let io =
                    pumpFibers
                    |> Flow.traverse Fiber.join
                    |> Flow.bind (fun _ ->
                        flow {
                            for target in stdoutTargets do
                                do! Flow.fromTask (fun token -> NativeProcess.finishTarget token target)
                            for stage in 0 .. stderrTargets.Length - 1 do
                                if not (specification.MergeStdErr && stage = final) then
                                    do! Flow.fromTask (fun token -> NativeProcess.finishTarget token stderrTargets[stage])
                        }
                        |> stopIo)

                let! ioFiber =
                    io
                    |> Flow.onExit stopWorkerFailure
                    |> Flow.forkNamed "process-io"
                let! inputFiber =
                    inputFibers
                    |> Seq.map awaitInput
                    |> List.ofSeq
                    |> Flow.sequencePar
                    |> Flow.ignore
                    |> Flow.onExit stopWorkerFailure
                    |> Flow.forkNamed "process-inputs"
                let! monitorFibers =
                    [ 0 .. processes.Count - 1 ]
                    |> Flow.traverse (monitor >> Flow.fork)

                let stopAndFail requested (error: ProcessError) =
                    flow {
                        let! winner = requestStop requested |> Flow.widenError
                        if winner = requested then return! Flow.fail error
                        else return! Flow.never<ClockEnvironment, ProcessError, _>
                    }

                let completeRun =
                    flow {
                        let! exitCodes = monitorFibers |> Flow.traverse Fiber.join

                        let finishIo =
                            flow {
                                do! Fiber.join ioFiber
                                do! Fiber.join inputFiber
                            }

                        match specification.DrainTimeout with
                        | Some after ->
                            let allEnded = elapsedEnds |> Array.choose id |> Array.max
                            let remaining = max TimeSpan.Zero (after - (clock.Elapsed() - allEnded))
                            do!
                                Flow.race
                                    finishIo
                                    (flow {
                                        do! Flow.sleep remaining
                                        return!
                                            stopAndFail
                                                (DrainTimeout after)
                                                (ProcessError.IoFailed {
                                                    Message =
                                                        String.Format(
                                                            Globalization.CultureInfo.InvariantCulture,
                                                            "Process I/O did not finish within {0} after all process stages exited.",
                                                            after
                                                        )
                                                })
                                    })
                        | None -> do! finishIo

                        match firstStop with
                        | Some(StageTimeout(stage, after)) ->
                            let! _ = Deferred.await stopFinished |> Flow.widenError
                            return!
                                Flow.fail (ProcessError.StageTimedOut {
                                    Stage = stage
                                    Command = Configuration.renderCommand commands[stage]
                                    Timeout = after
                                })
                        | Some(OverallTimeout after) ->
                            let! _ = Deferred.await stopFinished |> Flow.widenError
                            return!
                                Flow.fail (ProcessError.OverallTimedOut {
                                    Specification =
                                        commands
                                        |> Array.map Configuration.renderCommand
                                        |> String.concat " | "
                                    Timeout = after
                                })
                        | Some(DrainTimeout after) ->
                            let! _ = Deferred.await stopFinished |> Flow.widenError
                            return!
                                Flow.fail (ProcessError.IoFailed {
                                    Message =
                                        String.Format(
                                            Globalization.CultureInfo.InvariantCulture,
                                            "Process I/O did not finish within {0} after all process stages exited.",
                                            after
                                        )
                                })
                        | _ -> ()

                        let result =
                            NativeProcess.buildResult clock specification commands processes starts elapsedStarts elapsedEnds stdout stderr startedAt startedElapsed

                        match
                            exitCodes
                            |> List.mapi (fun stage code -> stage, code)
                            |> List.tryFind (fun (stage, code) -> not (commands[stage].SuccessCodes.Value.Contains code))
                        with
                        | Some(stage, _) ->
                            return! Flow.fail (ProcessError.StageFailed { Stage = result.Stages[stage]; Result = result })
                        | None -> return result
                    }

                let execution =
                    if timerFibers.Count = 0 then
                        completeRun
                    else
                        let timerFailure =
                            timerFibers
                            |> Seq.map Fiber.join
                            |> List.ofSeq
                            |> Flow.sequencePar
                            |> Flow.bind (fun _ -> Flow.never<ClockEnvironment, ProcessError, ProcessResult>)
                        Flow.race completeRun timerFailure

                let! result =
                    execution
                    |> Flow.onExit (function
                        | Exit.Failure cause when not (Cause.isInterrupted cause) ->
                            requestStop WorkerFailure |> Flow.ignore
                        | _ -> Flow.succeed ())
                    |> Flow.onInterrupt (requestStop Interrupted |> Flow.ignore)

                do! Dequeue.shutdown commandsQueue
                let! _ = Fiber.await coordinator
                return result
            }
            |> mapEngineFailure
            |> Flow.scoped

        run |> Flow.localEnv (fun () -> clockEnvironment)
