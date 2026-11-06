// axial-allow-file-AXG001: ProcessRun is the live IProcess implementation and owns native process handles
namespace Axial.Process

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console
open Axial.FileSystem
open Axial

#if !FABLE_COMPILER
exception private ProcessPumpFailure of ProcessError
exception private ChildClosedStdin

type private StopCause =
    | Interrupted
    | StageTimeout of stage: int * after: TimeSpan
    | OverallTimeout of after: TimeSpan
    | DrainTimeout of after: TimeSpan
    | WorkerFailure
    | ExternalClose

type private RunStage = {
    Process: Diagnostics.Process
    Pidfd: int
    Exit: Deferred<Never, int>
    mutable Exited: bool
    mutable StdinClosed: bool
    mutable FeederClosedPipe: bool
    mutable Feeder: Fiber<ProcessError, unit> option
}

type private RunCommand =
    | RegisterFeeder of stage: int * feeder: Fiber<ProcessError, unit>
    | Close of stage: int * request: StopRequest * initiateShutdown: bool * reply: Deferred<StopRequestError, unit>
    | Exited of stage: int * code: int
    | Stop of cause: StopCause * reply: Deferred<Never, StopCause>

/// Flow-based process execution with scoped processes, supervised pumps, and one stop coordinator.
module internal ProcessRun =
    // Pass state explicitly so call-site actions are noncapturing, cached functions.
    let internal bestEffort state (action: 'state -> unit) =
        try action state
        with
        | :? OutOfMemoryException
        | :? StackOverflowException
        | :? AccessViolationException -> reraise ()
        | _ ->
            // axial-note AXG010: shutdown cleanup is best-effort; ordinary pipe, process or timer failures must not replace the original outcome.
            ()

    let internal classifyInputExit =
        function
        | Exit.Success () -> Exit.Success ()
        | Exit.Failure cause when not (List.isEmpty (Cause.defects cause)) -> Exit.Failure cause
        | Exit.Failure cause when not (List.isEmpty (Cause.failures cause)) -> Exit.Failure cause
        | Exit.Failure cause when Cause.isInterrupted cause -> Exit.Success ()
        | Exit.Failure cause -> Exit.Failure cause

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
                    if proc.Start() then
                        Flow.succeed proc
                    else
                        proc.Dispose()
                        Flow.fail (processError command (InvalidOperationException "Process.Start returned false."))
                with error ->
                    proc.Dispose()
                    Flow.fail (processError command error))

    let private releaseStage (proc: Diagnostics.Process) : Flow<unit, Never, unit> =
        Flow.delay (fun () ->
            bestEffort proc (fun proc ->
                if not proc.HasExited then
#if NETSTANDARD2_1
                    proc.Kill()
#else
                    // OS bridge: abnormal scope close must terminate the owned process tree.
                    proc.Kill(entireProcessTree = true)
#endif
            )

            bestEffort proc (fun proc ->
                if proc.StartInfo.RedirectStandardInput then proc.StandardInput.BaseStream.Dispose()
                if proc.StartInfo.RedirectStandardOutput then proc.StandardOutput.BaseStream.Dispose()
                if proc.StartInfo.RedirectStandardError then proc.StandardError.BaseStream.Dispose())

            proc.Dispose()
            Flow.succeed ())

    let private waitForStage (proc: Diagnostics.Process) =
        // Task bridge: WaitForExitAsync is the OS completion notification.
        Flow.fromTask (fun token -> task {
            do! NativeProcess.waitForExit proc token
            return proc.ExitCode
        })

    let private readChunk (stream: Stream) (buffer: byte array) =
        Flow.fromValueTask (fun token -> stream.ReadAsync(buffer.AsMemory(), token))

    let private closeInput (proc: Diagnostics.Process) =
        Flow.delay (fun () ->
            bestEffort proc (fun proc -> proc.StandardInput.Close())
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
                if hasTypedFailure cause && (Cause.defects cause |> List.isEmpty) then
                    Flow.fail (ProcessError.InputFailed(cause.ToString()))
                else
                    Flow.ofExit (Exit.Failure(Cause.map ProcessError.InputFailed cause)))

    let private mapEngineFailure (source: Flow<unit, ProcessError, 'value>) =
        source
        |> Flow.foldCause
            Flow.succeed
            (fun cause ->
                match Cause.untraced cause with
                | Cause.Die (:? ProcessFailureException as failure) -> Flow.fail failure.Error
                | Cause.Die (:? ObserverDefectException as defect) -> Flow.die defect.InnerException
                | _ -> Flow.ofExit (Exit.Failure cause))

    let execute
        (fileSystem: IFileSystem)
        (console: IConsole)
        (specification: ProcessSpec)
        (observe: ProcessOutput -> Async<unit>)
        : Flow<unit, ProcessError, ProcessResult> =
        let observesOutput = not (obj.ReferenceEquals(observe, ProcessObserver.ignore))

        let run (caller: RuntimeContext) : Flow<unit, ProcessError, ProcessResult> =
            let clock = caller.Platform.Clock
            flow {
                let commands = specification.Commands |> List.toArray
                let stdoutLimit, stderrLimit = specification.MaxOutputBytes

                for command in commands do
                    if stdoutLimit.IsSome && NativeProcess.inherited command.StdOut.Value then
                        do! Flow.die (ArgumentException("A stdout byte cap cannot be enforced on inherited stdout.", "specification"))
                    if stderrLimit.IsSome && NativeProcess.inherited command.StdErr.Value then
                        do! Flow.die (ArgumentException("A stderr byte cap cannot be enforced on inherited stderr.", "specification"))

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

                let! exits =
                    commands
                    |> Flow.traverse (fun _ -> Deferred.make<unit, Never, int> ())
                    |> Flow.widenError
                let! (commandsQueue: Queue<RunCommand>) = Queue.unbounded ()
                let timerFibers = ResizeArray<Fiber<ProcessError, unit>>()

                let postTimedStop cause error =
                    flow {
                        let! reply = Deferred.make<unit, Never, StopCause> () |> Flow.widenError
                        let! _ = commandsQueue |> Queue.offer (Stop(cause, reply))
                        let! winner = Deferred.await reply |> Flow.widenError
                        if winner = cause then return! Flow.fail error
                        // A losing timer is finished, not a permanently suspended owned child.
                        // The execution race keeps waiting for completeRun, but scope close must
                        // be able to join this timer after an external Close wins.
                        else return ()
                    }

                let sleepStartedNow (delay: TimeSpan) =
                    let cancellation = new CancellationTokenSource()
                    let pending = clock.Sleep(delay, cancellation.Token)
                    let cancel =
                        Flow.delay (fun () ->
                            bestEffort cancellation (fun cancellation -> cancellation.Cancel())
                            Flow.succeed ())
                    let sleeping =
                        Flow.fromTask (fun token -> task {
                            let interrupted = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                            use _registration = token.Register(fun () -> interrupted.TrySetResult() |> ignore)
                            let! first = Task.WhenAny(pending, interrupted.Task)
                            if obj.ReferenceEquals(first, interrupted.Task) then cancellation.Cancel()
                            try
                                do! pending
                                return true
                            with :? OperationCanceledException when cancellation.IsCancellationRequested ->
                                return false
                        })
                        |> Flow.ensuring (
                            Flow.delay (fun () ->
                                bestEffort cancellation (fun cancellation -> cancellation.Cancel())
                                cancellation.Dispose()
                                Flow.succeed ())
                        )
                    sleeping, cancel

                let startTimer cause error sleeping =
                    sleeping
                    |> Flow.bind (fun timedOut ->
                        if timedOut then postTimedStop cause error
                        else Flow.succeed ())

                let mutable cancelOverallTimer = Flow.succeed ()
                match specification.OverallTimeout with
                | Some after ->
                    let remaining = max TimeSpan.Zero (after - (clock.Elapsed() - startedElapsed))
                    let error =
                        ProcessError.OverallTimedOut {
                            Specification = commands |> Array.map Configuration.renderCommand |> String.concat " | "
                            Timeout = after
                        }
                    let sleeping, cancelSleep = sleepStartedNow remaining
                    cancelOverallTimer <- cancelSleep
                    let! timer = startTimer (OverallTimeout after) error sleeping |> Flow.forkNamed "process-overall-timeout"
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
                        let sleeping, cancelSleep = sleepStartedNow after
                        let timeout =
                            Flow.race
                                (Deferred.await exits[stage]
                                 |> Flow.widenError
                                 |> Flow.ensuring cancelSleep
                                 |> Flow.map (fun _ -> false))
                                sleeping
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
                                    Flow.succeed ())
                        let! timer = timeout |> Flow.forkNamed ("process-stage-timeout-" + string stage)
                        timerFibers.Add timer
                    | None -> ()

                let stages =
                    processes
                    |> Seq.mapi (fun stage proc -> {
                        Process = proc
                        Pidfd = NativeProcess.tryOpenPidfd proc.Id
                        Exit = exits[stage]
                        Exited = false
                        StdinClosed = false
                        FeederClosedPipe = false
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
                        bestEffort stage (fun stage -> stage.Process.StandardInput.BaseStream.Dispose())
                        bestEffort stage (fun stage -> stage.Process.StandardInput.Close())
                        Flow.succeed ())

                // Stops input to a stage without waiting: the coordinator must stay responsive. The feeder is
                // asked to stop and its pipe is closed, which also releases a write blocked on a full pipe. Whoever
                // owns the feeder's outcome waits for it: awaitInput while the run is live, the run's scope
                // finalizer otherwise. Interruption and the pipe-closed errors this causes are not failures (see
                // inputFeeder); any other failure or defect the feeder settles with is reported.
                let stopFeeder (stage: RunStage) =
                    flow {
                        match stage.Feeder with
                        | Some feeder when not stage.FeederClosedPipe ->
                            do! Fiber.requestInterrupt feeder
                            do! closePipe stage
                        | Some _ -> ()
                        | None -> do! closePipe stage
                    }

                let completeClose result reply =
                    match result with
                    | Ok () -> Deferred.succeed () reply |> Flow.ignore
                    | Error error -> Deferred.fail error reply |> Flow.ignore

                let isLive (stage: RunStage) =
                    // axial-note AXG010: an inaccessible or disposed process handle is treated as not live during shutdown, not as a new pipeline failure.
                    try not stage.Process.HasExited with _ -> false

                let terminateAll () =
                    flow {
                        do!
                            Flow.delay (fun () ->
                                for stage in stages do
                                    bestEffort stage (fun stage ->
#if NETSTANDARD2_1
                                        stage.Process.Kill()
#else
                                        // A failed pump may race the direct child's exit while a descendant still
                                        // owns its pipes, so abnormal termination always attempts the whole tree.
                                        stage.Process.Kill(entireProcessTree = true)
#endif
                                    )
                                    bestEffort stage (fun stage ->
                                        if stage.Process.StartInfo.RedirectStandardOutput then stage.Process.StandardOutput.BaseStream.Dispose()
                                        if stage.Process.StartInfo.RedirectStandardError then stage.Process.StandardError.BaseStream.Dispose())
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

                // Close has already delivered its stage-local request. Reuse that delivery for
                // the first matching policy step rather than signalling that stage twice.
                let mutable initialClose: (int * StopRequest) option = None
                let runStopLadder () =
                    flow {
                        for step, (request, grace) in List.indexed specification.StopGracefully do
                            let live = stages |> Array.filter isLive
                            let! sent =
                                live
                                |> Array.map (fun stage ->
                                    match initialClose with
                                    | Some(index, sent) when step = 0 && sent = request && obj.ReferenceEquals(stage, stages[index]) ->
                                        Flow.succeed true
                                    | _ -> sendRequest request stage)
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
                    Deferred.make<unit, Never, StopCause> ()
                    |> Flow.widenError
                let! ioStop =
                    Deferred.make<unit, Never, unit> ()
                    |> Flow.widenError

                let mutable stopLadder: Fiber<Never, unit> option = None
                let stop cause reply =
                    flow {
                        match firstStop with
                        | Some _ -> ()
                        | None ->
                            firstStop <- Some cause
                            Deferred.succeedNow () ioStop |> ignore
                            let! child =
                                flow {
                                    do! runStopLadder ()
                                    if cause = ExternalClose then do! cancelOverallTimer
                                    let! _ = Deferred.succeed cause stopFinished
                                    return ()
                                }
                                |> Flow.forkNamed "process-stop-ladder"
                            stopLadder <- Some child
                        // Stop callers await cleanup; Close callers only await acceptance. Neither
                        // wait may occupy the command loop while the ladder's clock is parked.
                        let! _ =
                            flow {
                                let! winner = Deferred.await stopFinished
                                let! _ = Deferred.succeed winner reply
                                return ()
                            }
                            |> Flow.forkNamed "process-stop-reply"
                        return ()
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
                                let! ignored = Deferred.make<unit, Never, StopCause> ()
                                do! stop WorkerFailure ignored
                        | Close(stage, request, initiateShutdown, reply) ->
                            let state = stages[stage]
                            if (initiateShutdown && firstStop.IsSome) || state.Exited || not (isLive state) then
                                do! completeClose (Ok ()) reply
                            else
                                let! result =
                                    if request = StopRequest.CloseStdIn then
                                        if stdinOwned[stage] then
                                            stopFeeder state |> Flow.map (fun () -> Ok ())
                                        else
                                            Flow.succeed (Error StopRequestError.Unsupported)
                                    else
                                        Flow.succeed (NativeProcess.sendStopRequest state.Process state.Pidfd request)
                                do! completeClose result reply
                                match result with
                                | Ok () when initiateShutdown ->
                                    initialClose <- Some(stage, request)
                                    let! ignored = Deferred.make<unit, Never, StopCause> ()
                                    do! stop ExternalClose ignored
                                | _ -> ()
                        | Stop(cause, reply) -> do! stop cause reply
                        return! coordinate ()
                    }

                // The coordinator and its supervised ladder outlive caller cancellation. Queue
                // shutdown ends acceptance, then joins the ladder before closing the child scope:
                // neither scope interruption nor a fixed cleanup bound may cut grace periods short.
                let coordinatorWork =
                    coordinate ()
                    |> Flow.ensuring (flow {
                        match stopLadder with
                        | Some child -> do! Fiber.join child
                        | None -> ()
                    })
                    |> Flow.scoped
                let! coordinator =
                    Flow(fun environment _ ->
                        coordinatorWork.Invoke(environment, Platform.Cancellation.never))
                    |> Flow.forkUnownedNamed "process-coordinator"

                let! () =
                    Flow.scopeFinalizer (
                        flow {
                            do! Dequeue.shutdown commandsQueue
                            let! _ = Fiber.awaitUnobserved coordinator
                            do! Fiber.reportUnobserved coordinator
                        }
                    )

                let requestStop cause =
                    flow {
                        let! reply = Deferred.make<unit, Never, StopCause> ()
                        let! offered =
                            (commandsQueue |> Queue.offer (Stop(cause, reply)) |> Flow.map Some)
                            |> Flow.foldCause Flow.succeed (fun _ -> Flow.succeed None)
                        match offered with
                        | Some _ -> return! Deferred.await reply
                        | None -> return cause
                    }

                let closeCallback initiateShutdown stage request = async {
                    let requestFlow =
                        flow {
                            let! reply = Deferred.make<unit, StopRequestError, unit> ()
                            let! _ = commandsQueue |> Queue.offer (Close(stage, request, initiateShutdown, reply))
                            return!
                                Deferred.await reply
                                |> Flow.map (fun () -> Ok ())
                                |> Flow.recover (Error >> Flow.succeed)
                        }
                        |> Flow.foldCause Flow.succeed (fun _ -> Flow.succeed (Ok ()))

                    let! token = Async.CancellationToken
                    let! exit =
                        FlowInternal.invoke (NativeProcess.withCallerServices caller requestFlow) () token
                        |> Platform.executionToAsync
                    return
                        match exit with
                        | Exit.Success result -> result
                        | Exit.Failure _ -> Ok ()
                }

                match specification.OnStarted with
                | Some handler ->
                    for stage, command in commands |> Array.indexed do
                        do!
                            handler.Invoke(
                                stage,
                                processes[stage].Id,
                                Configuration.renderCommand command,
                                starts[stage],
                                closeCallback true stage,
                                closeCallback false stage
                            )
                            |> Flow.scoped
                            |> NativeProcess.withCallerServices caller
                            |> Flow.onInterrupt (requestStop Interrupted |> Flow.ignore)
                            |> Flow.widenError
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

                let outputPump stage original observed target (capture: CaptureBuffer) (stream: Stream) =
                    // OS I/O bridge: one task owns the pipe read loop; Flow owns cancellation and failure handling.
                    Flow.fromTask (fun token -> task {
                        let buffer = Buffers.ArrayPool<byte>.Shared.Rent 81920
                        let mutable rentedChars: char array = null
                        try
                            let decoding = observesOutput || specification.Framing = OutputFraming.Lines
                            let decoder = if decoding then commands[stage].Encoding.Value.GetDecoder() else null
                            let chars =
                                if decoding then
                                    rentedChars <- Buffers.ArrayPool<char>.Shared.Rent(commands[stage].Encoding.Value.GetMaxCharCount buffer.Length)
                                    rentedChars
                                else null
                            let pending = if decoding then StringBuilder() else null
                            let gate = if observed = OutputChannel.StdOut then outputGates[stage] else errorGates[stage]

                            let publish text =
                                NativeProcess.publishObservation (clock.UtcNow()) observe observerGate token stage original observed text

                            let decode count flush = task {
                                let length = decoder.GetChars(buffer, 0, count, chars, 0, flush)
                                match specification.Framing with
                                | OutputFraming.Chunks ->
                                    if length > 0 then do! publish (String(chars, 0, length))
                                | OutputFraming.Lines ->
                                    for index in 0 .. length - 1 do
                                        let value = chars[index]
                                        if value = '\n' then
                                            if pending.Length > 0 && pending[pending.Length - 1] = '\r' then
                                                pending.Length <- pending.Length - 1
                                            do! publish (pending.ToString())
                                            pending.Clear() |> ignore
                                        else
                                            pending.Append value |> ignore
                                            if pending.Length > specification.MaxLineLength
                                               && not (pending.Length = specification.MaxLineLength + 1 && value = '\r') then
                                                // axial-allow-AXG003: task bridge translates this back to the typed channel
                                                raise (ProcessPumpFailure (ProcessError.LineTooLong {
                                                    Stage = stage; OriginalChannel = original; Limit = specification.MaxLineLength }))
                                    if flush && pending.Length > 0 then
                                        if pending.Length > specification.MaxLineLength then
                                            // axial-allow-AXG003: task bridge translates this back to the typed channel
                                            raise (ProcessPumpFailure (ProcessError.LineTooLong {
                                                Stage = stage; OriginalChannel = original; Limit = specification.MaxLineLength }))
                                        do! publish (pending.ToString())
                                        pending.Clear() |> ignore
                            }

                            let mutable reading = true
                            while reading do
                                let! count = stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)
                                if count = 0 then
                                    reading <- false
                                    if observesOutput || specification.Framing = OutputFraming.Lines then
                                        do! decode 0 true
                                else
                                    countBytes stage original count
                                    let bytes =
                                        if count = buffer.Length then buffer
                                        else buffer[.. count - 1]
                                    do! NativeProcess.writeCapturedTarget caller console files token gate observed target capture bytes
                                    if observesOutput || specification.Framing = OutputFraming.Lines then
                                        do! decode count false
                        finally
                            if not (isNull rentedChars) then Buffers.ArrayPool<char>.Shared.Return rentedChars
                            Buffers.ArrayPool<byte>.Shared.Return buffer
                    })
                    |> Flow.foldCause Flow.succeed (fun cause ->
                        match Cause.untraced cause with
                        | Cause.Die (ProcessPumpFailure error) -> Flow.fail error
                        | _ -> Flow.ofExit (Exit.Failure cause))

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
                        let buffer = Buffers.ArrayPool<byte>.Shared.Rent 81920
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
                        |> Flow.ensuring (
                            Flow.delay (fun () ->
                                pending.Dispose()
                                Buffers.ArrayPool<byte>.Shared.Return buffer
                                Flow.succeed ()))

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

                let reads = ResizeArray<Flow<unit, ProcessError, unit>>()
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

                let childClosedPipe (error: IOException) =
                    match error.InnerException with
                    | :? System.Net.Sockets.SocketException as socket -> socket.NativeErrorCode = 32 // Unix EPIPE
                    | _ ->
                        let code = error.HResult &&& 0xffff
                        code = 109 || code = 232 // Windows ERROR_BROKEN_PIPE / ERROR_NO_DATA

                let classifyInputWrite work =
                    work |> Flow.foldCause Flow.succeed (fun cause ->
                        match Cause.untraced cause with
                        | Cause.Die (:? IOException as error) when childClosedPipe error ->
                            // Stop feeding, not just this write. User source defects never pass through here.
                            Flow.die ChildClosedStdin
                        | _ -> Flow.ofExit (Exit.Failure cause))

                let writeInput stage bytes =
                    Flow.fromTask (fun token -> task {
                        do! processes[stage].StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, token)
                    })
                    |> classifyInputWrite

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
                            |> classifyInputWrite
                        | InputSource.Stream source ->
                            let work =
                                source
                                |> FlowStream.runForEachFlow (writeInput stage)
                                |> mapInputFailure

                            work

                    pumping
                    |> Flow.foldCause
                        Flow.succeed
                        (fun cause ->
                            let expectedPipeClose =
                                match Cause.untraced cause with
                                | Cause.Interrupt -> stages[stage].StdinClosed
                                | Cause.Die ChildClosedStdin -> true
                                | Cause.Die error ->
                                    stages[stage].StdinClosed
                                    && (error :? ObjectDisposedException
                                        || error :? IOException
                                        || error :? OperationCanceledException)
                                | _ -> false
                            if expectedPipeClose then Flow.succeed ()
                            else Flow.ofExit (Exit.Failure cause))
                    |> Flow.ensuring (
                        Flow.delay (fun () ->
                            // This fact is published in the same finalizer step that closes the pipe. The
                            // enclosing fiber records its success or failure only after this step completes.
                            stages[stage].FeederClosedPipe <- true
                            stages[stage].StdinClosed <- true
                            bestEffort processes[stage] (fun proc -> proc.StandardInput.Close())
                            Flow.succeed ())
                        |> Flow.mapError Never.absurd
                    )

                let inputFibers = ResizeArray<int * Fiber<ProcessError, unit>>()

                for stage, command in commands |> Array.indexed do
                    if specification.Connections |> List.forall (fun (_, target, _) -> target <> stage) then
                        match command.StdIn.Value with
                        | InputSource.Inherit -> ()
                        | InputSource.Empty ->
                            stages[stage].StdinClosed <- true
                            do! closeInput processes[stage]
                        | source ->
                            let! feeder = inputFeeder stage source |> Flow.forkUnownedNamed ("process-stdin-" + string stage)
                            inputFibers.Add(stage, feeder)
                            let! _ = commandsQueue |> Queue.offer (RegisterFeeder(stage, feeder))
                            ()

                // Waits for a feeder without a deadline, like the process itself: a run never returns while
                // its input is still being written. A source that ignores interruption keeps the run waiting;
                // wrap such a source in Flow.disconnect to let the run return without it.
                let awaitInput (_stage: int, feeder) =
                    Fiber.awaitUnobserved feeder
                    |> Flow.bind (fun exit ->
                        Fiber.markObserved feeder
                        |> Flow.bind (fun () -> Flow.ofExit (classifyInputExit exit)))

                // On every exit path, including interruption, stop each feeder and wait for it before the run's
                // scope closes. A defect that no awaitInput consumed is reported once.
                let! () =
                    Flow.scopeFinalizer (
                        Flow.delay (fun () ->
                            inputFibers
                            |> Seq.map (fun (stage, feeder) ->
                                flow {
                                    do! stopFeeder stages[stage]
                                    let! _ = Fiber.awaitUnobserved feeder
                                    do! Fiber.reportUnobserved feeder
                                })
                            |> List.ofSeq
                            |> Flow.sequence
                            |> Flow.ignore)
                    )

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
                                do! Flow.fromTask (fun token -> NativeProcess.finishTarget caller token target)
                            for stage in 0 .. stderrTargets.Length - 1 do
                                if not (specification.MergeStdErr && stage = final) then
                                    do! Flow.fromTask (fun token -> NativeProcess.finishTarget caller token stderrTargets[stage])
                        }
                        |> stopIo)

                let! ioFiber =
                    io
                    |> Flow.onExit stopWorkerFailure
                    |> Flow.forkNamed "process-io"
                let! inputFiber =
                    if inputFibers.Count = 0 then
                        Flow.succeed None
                    else
                        inputFibers
                        |> Seq.map awaitInput
                        |> List.ofSeq
                        |> Flow.sequencePar
                        |> Flow.ignore
                        |> Flow.onExit stopWorkerFailure
                        |> Flow.forkNamed "process-inputs"
                        |> Flow.map Some
                let! monitorFibers =
                    [ 0 .. processes.Count - 1 ]
                    |> Flow.traverse (monitor >> Flow.fork)

                let stopAndFail requested (error: ProcessError) =
                    flow {
                        let! winner = requestStop requested |> Flow.widenError
                        if winner = requested then return! Flow.fail error
                        else return! Flow.never<unit, ProcessError, _>
                    }

                let completeRun =
                    flow {
                        let! exitCodes = monitorFibers |> Flow.traverse Fiber.join

                        let finishIo =
                            flow {
                                do! Fiber.join ioFiber
                                match inputFiber with
                                | Some feeder -> do! Fiber.join feeder
                                | None -> ()
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
                        | Some ExternalClose ->
                            let! _ = Deferred.await stopFinished |> Flow.widenError
                            ()
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
                            NativeProcess.buildResult (clock.Elapsed()) specification commands processes starts elapsedStarts elapsedEnds stdout stderr startedAt startedElapsed

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
                            |> Flow.bind (fun _ -> Flow.never<unit, ProcessError, ProcessResult>)
                        Flow.race completeRun timerFailure

                let! result =
                    execution
                    |> Flow.onExit (function
                        | Exit.Failure cause when not (Cause.isInterrupted cause) ->
                            requestStop WorkerFailure |> Flow.ignore
                        | _ -> Flow.succeed ())
                    |> Flow.onInterrupt (requestStop Interrupted |> Flow.ignore)

                do! Dequeue.shutdown commandsQueue
                let! _ = Fiber.awaitExit coordinator
                return result
            }
            |> mapEngineFailure
            |> Flow.scoped

        Flow(fun environment token ->
            let caller = RuntimePlatform.current ()
            FlowInternal.invoke (run caller) environment token)
    let private launchCommand fileName arguments : CommandDefinition = {
        FileName = fileName
        Arguments = arguments
        RedactedArguments = Map.empty
        WorkingDirectory = None
        Environment = Map.empty
        Encoding = None
        SuccessCodes = None
        Timeout = None
        StdIn = None
        StdOut = None
        StdErr = None
        StdOutCapture = None
        StdErrCapture = None
    }

    /// The start info for a detached launch. Unlike a captured run, a launched console program gets its own window.
    let launchStartInfo (request: ProcessLaunch) : ProcessStartInfo =
        match request with
        | ProcessLaunch.Command(ProcessCommand command) ->
            let info = NativeProcess.startInfo false false false command
            info.CreateNoWindow <- false
            info
        | ProcessLaunch.ShellOpen target when RuntimeInformation.IsOSPlatform OSPlatform.Windows ->
            let info = ProcessStartInfo(target)
            info.UseShellExecute <- true
            info
        | ProcessLaunch.ShellOpen target ->
            let opener = if RuntimeInformation.IsOSPlatform OSPlatform.OSX then "open" else "xdg-open"
            let info = NativeProcess.startInfo false false false (launchCommand opener [ target ])
            info.CreateNoWindow <- false
            info

    /// Starts a detached child and disposes its handle at once; the child keeps running and is not awaited.
    let launch (request: ProcessLaunch) : Flow<unit, ProcessError, unit> =
        Flow.delay (fun () ->
            let checkedTarget =
                match request with
                | ProcessLaunch.ShellOpen target -> ProcessLaunchTarget.validate target
                | ProcessLaunch.Command _ -> Ok ()
            match checkedTarget with
            | Error error -> Flow.fail error
            | Ok () ->
                let description =
                    match request with
                    | ProcessLaunch.Command(ProcessCommand command) -> Configuration.renderCommand command
                    | ProcessLaunch.ShellOpen target when RuntimeInformation.IsOSPlatform OSPlatform.Windows -> target
                    | ProcessLaunch.ShellOpen target ->
                        let opener = if RuntimeInformation.IsOSPlatform OSPlatform.OSX then "open" else "xdg-open"
                        Configuration.renderCommand (launchCommand opener [ target ])
                try
                    use proc = new Diagnostics.Process(StartInfo = launchStartInfo request)
                    // OS bridge: Process.Start is the process service's named operational boundary. A shell open that
                    // an already-running handler accepts returns false without a new process; that is still a launch.
                    proc.Start() |> ignore
                    Flow.ok ()
                with error ->
                    Flow.fail (ProcessError.StartFailed { Command = description; Message = error.Message }))
#endif
