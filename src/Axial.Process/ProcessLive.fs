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
open Axial.PlatformService
open Axial.Console
open Axial.FileSystem

#if !FABLE_COMPILER
module internal NativeProcess =
    [<DllImport("libc", SetLastError = true)>]
    extern int private kill(int pid, int signal)

    [<DllImport("libc", SetLastError = true)>]
    extern int private pidfd_open(int pid, uint flags)

    [<DllImport("libc", SetLastError = true)>]
    extern int private pidfd_send_signal(int pidfd, int signal, nativeint info, uint flags)

    [<DllImport("libc", EntryPoint = "close", SetLastError = true)>]
    extern int private closeFd(int fd)

    /// How long CloseStdIn waits for the stdin feeder to observe cancellation before it force-closes
    /// the pipe. Keeps Close from hanging on a feeder blocked by a full pipe.
    let private closeStdInFeederBound = TimeSpan.FromMilliseconds 250.0

    /// Reports whether pidfd_open/pidfd_send_signal are usable on this Linux system. Tests use this to
    /// assert the pidfd signalling path is active when the platform supports it.
    let pidfdAvailable () =
        if not (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) then
            false
        else
            try
                let fd = pidfd_open(Diagnostics.Process.GetCurrentProcess().Id, 0u)

                if fd >= 0 then
                    closeFd(fd) |> ignore
                    true
                else
                    false
            with :? EntryPointNotFoundException -> false

    /// Sends a signal to a process through pidfd only (no kill fallback). Tests use this to prove the
    /// pidfd path delivers a signal end-to-end on Linux.
    let trySignalWithPidfd (pid: int) (signal: int) : Result<unit, string> =
        if not (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) then
            Error "pidfd signalling is available only on Linux."
        else
            try
                let fd = pidfd_open(pid, 0u)

                if fd < 0 then
                    Error (System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message)
                else
                    try
                        if pidfd_send_signal(fd, signal, 0n, 0u) = 0 then
                            Ok ()
                        else
                            Error (System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message)
                    finally
                        closeFd(fd) |> ignore
            with :? EntryPointNotFoundException -> Error "pidfd_open is not exported by this libc."

    let internal tryOpenPidfd pid =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            try pidfd_open(pid, 0u)
            with :? EntryPointNotFoundException -> -1
        else
            -1

    let internal closePidfd fd =
        if fd >= 0 then closeFd(fd) |> ignore

    let internal sendStopRequest (proc: Diagnostics.Process) pidfd request =
        if request = StopRequest.CloseMainWindow then
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                try
                    if proc.CloseMainWindow() then Ok ()
                    else Error (StopRequestError.Failed "The process has no main window.")
                with error -> Error (StopRequestError.Failed error.Message)
            else
                Error StopRequestError.Unsupported
        elif RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            Error StopRequestError.Unsupported
        else
            let signal =
                match request with
                | StopRequest.Interrupt -> 2
                | StopRequest.Terminate -> 15
                | StopRequest.Hangup -> 1
                | StopRequest.Signal value -> value
                | _ -> 0

            if signal = 0 then
                Error StopRequestError.Unsupported
            else
                let mutable sent = -1
                let mutable error = 38

                if pidfd >= 0 then
                    try
                        sent <- pidfd_send_signal(pidfd, signal, 0n, 0u)
                        if sent <> 0 then error <- Marshal.GetLastWin32Error()
                    with :? EntryPointNotFoundException -> ()

                // PID fallback has an unavoidable reuse window on platforms without pidfd support.
                if sent <> 0 && (pidfd < 0 || error = 38) then
                    sent <- kill(proc.Id, signal)
                    if sent <> 0 then error <- Marshal.GetLastWin32Error()

                if sent = 0 || error = 3 then Ok ()
                else Error (StopRequestError.Failed(System.ComponentModel.Win32Exception(error).Message))

    let internal inherited =
        function
        | OutputTarget.Inherit -> true
        | _ -> false

    let internal startInfo redirectInput redirectOutput redirectError (command: CommandDefinition) =
        let info = ProcessStartInfo(command.FileName)
        command.Arguments |> List.iter info.ArgumentList.Add

        command.WorkingDirectory
        |> Option.iter (fun path -> info.WorkingDirectory <- path)

        command.Environment
        |> Map.iter (fun name value ->
            match value with
            | Some text -> info.Environment[name] <- text
            | None -> info.Environment.Remove name |> ignore)

        info.RedirectStandardInput <- redirectInput
        info.RedirectStandardOutput <- redirectOutput
        info.RedirectStandardError <- redirectError
        info.UseShellExecute <- false
        info.CreateNoWindow <- true
        info

    let internal waitForExit (proc: Diagnostics.Process) token =
#if NETSTANDARD2_1
        Task.Run((fun () -> proc.WaitForExit()), token)
#else
        proc.WaitForExitAsync(token)
#endif
    let rec private instantiate =
        function
        | OutputTarget.SinkFactory create ->
            let write, finish = create ()
            OutputTarget.Sink(write, finish)
        | OutputTarget.Tee targets -> OutputTarget.Tee(List.map instantiate targets)
        | target -> target

    let rec internal openTargets (fileSystem: IFileSystem) (files: Dictionary<string * bool, FileStream>) =
        function
        | OutputTarget.File path
        | OutputTarget.AppendFile path as target ->
            let append =
                match target with
                | OutputTarget.AppendFile _ -> true
                | _ -> false

            if not (files.ContainsKey(path, append)) then
                files.Add(
                    (path, append),
                    fileSystem.OpenFile(
                        path,
                        (if append then FileMode.Append else FileMode.Create),
                        FileAccess.Write,
                        FileShare.Read
                    )
                )
        | OutputTarget.Tee targets -> List.iter (openTargets fileSystem files) targets
        | _ -> ()

    let private invokeCallback (token: CancellationToken) (work: Async<unit>) =
        let result =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let fail (error: exn) =
            match error with
            | :? OperationCanceledException as canceled when ForeignCancellation.isOurs token canceled ->
                result.TrySetCanceled(token) |> ignore
            | _ -> result.TrySetException(ObserverDefectException error) |> ignore

        Async.StartWithContinuations(
            work,
            (fun () -> result.TrySetResult() |> ignore),
            fail,
            (fun canceled -> fail canceled),
            cancellationToken = token
        )

        result.Task

    let rec private hasTypedFailure (cause: Cause<'error>) =
        match cause with
        | Cause.Fail _ -> true
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> hasTypedFailure left || hasTypedFailure right
        | Cause.Traced(inner, _) -> hasTypedFailure inner
        | _ -> false

    let rec private firstDefect (cause: Cause<'error>) =
        match cause with
        | Cause.Die error -> Some error
        | Cause.Then(left, right)
        | Cause.Both(left, right) ->
            match firstDefect left with
            | Some error -> Some error
            | None -> firstDefect right
        | Cause.Traced(inner, _) -> firstDefect inner
        | _ -> None

    let private raiseStartedHandlerFailure (token: CancellationToken) (cause: Cause<Never>) =
        match firstDefect cause with
        | Some error -> raise (ObserverDefectException error)
        | None ->
            token.ThrowIfCancellationRequested()
            raise (OperationCanceledException token)

    // Raises the exception a failed user input or output flow maps to. Kept out of `runUserFlow`'s `task { }`: the
    // F# compiler in SDK 10.0.4xx emits invalid IL for local recursive functions inside that block.
    let private raiseUserFlowFailure (token: CancellationToken) input (cause: Cause<string>) : unit =
        if hasTypedFailure cause then
            raise (
                ProcessFailureException(
                    if input then
                        ProcessError.InputFailed(cause.ToString())
                    else
                        ProcessError.OutputFailed(cause.ToString())
                )
            )
        else
            match firstDefect cause with
            | Some error -> raise (ObserverDefectException error)
            | None ->
                token.ThrowIfCancellationRequested()
                raise (ObserverDefectException(Exception(cause.ToString())))

    let private runUserFlow token input (work: Flow<unit, string, unit>) =
        task {
            let! result = Async.StartAsTask(Flow.toAsync () work, cancellationToken = token)

            match result with
            | Exit.Success() -> ()
            | Exit.Failure cause -> raiseUserFlowFailure token input cause
        }

    let rec private writeTarget
        (console: IConsole)
        (files: Dictionary<string * bool, FileStream>)
        (token: CancellationToken)
        channel
        target
        (bytes: byte array)
        =
        (task {
            match target with
            | OutputTarget.Console ->
                let stream =
                    if channel = OutputChannel.StdOut then
                        console.OpenStandardOutput()
                    else
                        console.OpenStandardError()

                do! stream.WriteAsync(bytes, 0, bytes.Length, token)
            | OutputTarget.File path -> do! files[path, false].WriteAsync(bytes, 0, bytes.Length, token)
            | OutputTarget.AppendFile path -> do! files[path, true].WriteAsync(bytes, 0, bytes.Length, token)
            | OutputTarget.Callback callback
            | OutputTarget.Sink(callback, _) -> do! runUserFlow token false (callback bytes)
            | OutputTarget.Tee targets ->
                for item in targets do
                    do! writeTarget console files token channel item bytes
            | _ -> ()
        })

    let internal writeCapturedTarget
        (console: IConsole)
        (files: Dictionary<string * bool, FileStream>)
        (token: CancellationToken)
        (gate: SemaphoreSlim)
        channel
        target
        (capture: CaptureBuffer)
        bytes
        =
        task {
            do! gate.WaitAsync token

            try
                capture.Append bytes
                do! writeTarget console files token channel target bytes
            finally
                gate.Release() |> ignore
        }

    let internal publishObservation
        (clock: IClock)
        (observe: ProcessOutput -> Async<unit>)
        (observerGate: SemaphoreSlim)
        (token: CancellationToken)
        stage
        original
        observed
        text
        =
        task {
            do! observerGate.WaitAsync token

            try
                do!
                    invokeCallback
                        token
                        (observe {
                            Stage = stage
                            OriginalChannel = original
                            ObservedChannel = observed
                            Text = text
                            Timestamp = clock.UtcNow()
                        })
            finally
                observerGate.Release() |> ignore
        }

    let rec internal finishTarget (token: CancellationToken) target =
        (task {
            match target with
            | OutputTarget.Sink(_, finish) -> do! runUserFlow token false (finish ())
            | OutputTarget.Tee targets ->
                for item in targets do
                    do! finishTarget token item
            | _ -> ()
        })

    let internal prepareTargetsAndCaptures (specification: ProcessSpec) (commands: CommandDefinition array) =
        let stdoutTargets = commands |> Array.map (fun command -> instantiate command.StdOut.Value)
        let stderrTargets = commands |> Array.map (fun command -> instantiate command.StdErr.Value)

        let stdout =
            commands
            |> Array.mapi (fun stage command ->
                let available =
                    not (inherited command.StdOut.Value)
                    && not (
                        specification.Connections
                        |> List.exists (fun (source, _, _) -> source = stage)
                    )

                new CaptureBuffer(
                    (if available then command.StdOutCapture.Value else OutputRetention.Nothing),
                    available
                ))

        let stderr =
            commands
            |> Array.mapi (fun stage command ->
                let available =
                    not (inherited command.StdErr.Value)
                    && not (
                        specification.Connections
                        |> List.exists (fun (source, _, both) -> source = stage && both)
                    )
                    && not (specification.MergeStdErr && specification.Leaves.Contains stage)

                new CaptureBuffer(
                    (if available then command.StdErrCapture.Value else OutputRetention.Nothing),
                    available
                ))

        stdoutTargets, stderrTargets, stdout, stderr

    let internal countBytes
        (specification: ProcessSpec)
        (commands: CommandDefinition array)
        (byteCounts: int64 array array)
        stage
        channel
        count
        =
        let index = if channel = OutputChannel.StdOut then 0 else 1
        let stdoutLimit, stderrLimit = specification.MaxOutputBytes
        let limit = if index = 0 then stdoutLimit else stderrLimit

        lock byteCounts[stage] (fun () ->
            let current = byteCounts[stage][index]

            match limit with
            | Some maximum when int64 count > maximum - current ->
                raise (ProcessFailureException(ProcessError.OutputTooLarge {
                    Stage = stage
                    Channel = channel
                    Limit = maximum
                    Command = Configuration.renderCommand commands[stage]
                }))
            | _ -> byteCounts[stage][index] <- current + int64 count)

    let internal buildResult
        (clock: IClock)
        (specification: ProcessSpec)
        (commands: CommandDefinition array)
        (processes: ResizeArray<Diagnostics.Process>)
        (starts: ResizeArray<DateTimeOffset>)
        (elapsedStarts: ResizeArray<TimeSpan>)
        (elapsedEnds: TimeSpan option array)
        (stdout: CaptureBuffer array)
        (stderr: CaptureBuffer array)
        startedAt
        startedElapsed
        =
        let final = Set.minElement specification.Leaves

        let stages =
            commands
            |> Array.mapi (fun stage command ->
                let exitCode =
                    if stage < processes.Count && processes[stage].HasExited then
                        processes[stage].ExitCode
                    else
                        -1

                {
                    Stage = stage
                    ProcessId = (if stage < processes.Count then processes[stage].Id else -1)
                    Command = Configuration.renderCommand command
                    ExitCode = exitCode
                    Succeeded = command.SuccessCodes.Value.Contains exitCode
                    StartedAt = (if stage < starts.Count then starts[stage] else startedAt)
                    Duration =
                        (if stage < elapsedStarts.Count then
                             (defaultArg elapsedEnds[stage] (clock.Elapsed())) - elapsedStarts[stage]
                         else
                             TimeSpan.Zero)
                    StdOutCapture = stdout[stage].Finish command.Encoding.Value
                    StdErrCapture = stderr[stage].Finish command.Encoding.Value
                })
            |> Array.toList

        let contributors =
            stages
            |> List.filter (fun stage ->
                not (
                    specification.Connections
                    |> List.exists (fun (source, _, both) -> source = stage.Stage && both)
                )
                && not (specification.MergeStdErr && stage.Stage = final))

        let full =
            contributors
            |> List.forall (fun stage ->
                commands[stage.Stage].StdErrCapture.Value = OutputRetention.All)

        let parts =
            contributors
            |> List.map (fun stage -> commands[stage.Stage].Encoding.Value, stage.StdErrCapture)

        let allBytes = parts |> List.sumBy (fun (_, capture) -> capture.Bytes.Length)
        let mutable skip = if full then 0 else max 0 (allBytes - 65536)

        let kept =
            parts
            |> List.map (fun (encoding, capture) ->
                let removed = min skip capture.Bytes.Length
                skip <- skip - removed

                let bytes =
                    if removed = capture.Bytes.Length then [||] else capture.Bytes[removed..]

                encoding.GetString bytes, bytes)

        let errorCapture = {
            Text = kept |> List.map fst |> String.concat ""
            Bytes = kept |> List.map snd |> Array.concat
            Truncated =
                (not full && allBytes > 65536)
                || (contributors |> List.exists (fun stage -> stage.StdErrCapture.Truncated))
            Available = contributors |> List.forall (fun stage -> stage.StdErrCapture.Available)
        }

        let outputCapture = stages[final].StdOutCapture

        {
            ExitCode = stages[final].ExitCode
            ExitCodes = stages |> List.map _.ExitCode
            StdOut = outputCapture.Text
            StdErr = errorCapture.Text
            StdOutCapture = outputCapture
            StdErrCapture = errorCapture
            Stages = stages
            StartedAt = startedAt
            Duration = clock.Elapsed() - startedElapsed
        }

#endif
