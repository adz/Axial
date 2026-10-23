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

/// Flow-based process engine under construction. The test-only switch keeps the shipped path on the legacy engine
/// until stdin, stop coordination, and timeout slices are complete.
module internal ProcessRun =
    let private flowEngineVariable = "AXIAL_PROCESS_ENGINE"

    let isEnabled () =
        // Test-only migration switch; production selection does not depend on ambient environment configuration.
        Environment.GetEnvironmentVariable(flowEngineVariable) = "flow" // axial-allow-effect: environment

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
                    let stageStartedAt = clock.UtcNow()
                    starts.Add stageStartedAt
                    elapsedStarts.Add(clock.Elapsed())

                    match specification.OnStarted with
                    | Some handler ->
                        let close request = async {
                            if elapsedEnds[stage].IsSome then
                                return Ok ()
                            else
                                try
                                    if proc.HasExited then return Ok () else return Error StopRequestError.Unsupported
                                with :? InvalidOperationException ->
                                    // A retained handle outlived the stage's scoped Process object.
                                    return Ok ()
                        }
                        let! handlerExit = handler.Invoke(stage, proc.Id, Configuration.renderCommand command, stageStartedAt, close) |> Flow.fromAsync
                        do! Flow.ofExit handlerExit |> Flow.widenError
                    | None -> ()

                let monitor stage =
                    waitForStage processes[stage]
                    |> Flow.tap (fun _ ->
                        Flow.delay (fun () ->
                            elapsedEnds[stage] <- Some(clock.Elapsed())
                            Flow.succeed ()))

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
                                let bytes = buffer[.. count - 1]
                                do!
                                    Flow.fromTask (fun token ->
                                        NativeProcess.writeCapturedTarget console files token gate observed target capture bytes)
                                do! decode bytes count false
                                return! loop ()
                        }
                    loop ()

                let pipelinePump target connections =
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
                        let rec loop () =
                            flow {
                                let! count = readChunk stream buffer
                                if count > 0 then
                                    countBytes source channel count
                                    do! write buffer[.. count - 1]
                                    return! loop ()
                            }
                        loop ()

                    flow {
                        do!
                            connections
                            |> List.collect (fun (source, _, both) ->
                                [ yield copy source OutputChannel.StdOut processes[source].StandardOutput.BaseStream
                                  if both then
                                      yield copy source OutputChannel.StdErr processes[source].StandardError.BaseStream ])
                            |> Flow.sequencePar
                            |> Flow.ignore
                        gate.Dispose()
                        do! closeInput processes[target]
                    }

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

                // Slice B has no stdin feeders. Closing empty/unsupported redirected input prevents a basic child
                // from waiting forever; later slices replace this with feeder fibers and the Close coordinator.
                for stage, command in commands |> Array.indexed do
                    if specification.Connections |> List.forall (fun (_, target, _) -> target <> stage) then
                        match command.StdIn.Value with
                        | InputSource.Inherit -> ()
                        | _ -> do! closeInput processes[stage]

                let! pumpFibers =
                    copies @ List.ofSeq reads
                    |> Flow.traverse Flow.fork

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
                        })

                let! ioFiber = Flow.fork io
                let! monitorFibers =
                    [ 0 .. processes.Count - 1 ]
                    |> Flow.traverse (monitor >> Flow.fork)
                let! exitCodes = monitorFibers |> Flow.traverse Fiber.join
                do! Fiber.join ioFiber

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
            |> mapEngineFailure
            |> Flow.scoped

        run |> Flow.localEnv (fun () -> clockEnvironment)
