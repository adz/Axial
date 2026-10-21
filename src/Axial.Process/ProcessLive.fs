// axial-allow-file-AXG001: this file implements the explicit process boundary
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

    let private inherited =
        function
        | OutputTarget.Inherit -> true
        | _ -> false

    let private startInfo redirectInput redirectOutput redirectError (command: CommandDefinition) =
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

    let private waitForExit (proc: Diagnostics.Process) token =
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

    let rec private openTargets (fileSystem: IFileSystem) (files: Dictionary<string * bool, FileStream>) =
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

    let rec private finishTarget (token: CancellationToken) target =
        (task {
            match target with
            | OutputTarget.Sink(_, finish) -> do! runUserFlow token false (finish ())
            | OutputTarget.Tee targets ->
                for item in targets do
                    do! finishTarget token item
            | _ -> ()
        })

    /// Executes native processes; extra effects are explicit clock, filesystem and console services.
    let live (clock: IClock) (fileSystem: IFileSystem) (console: IConsole) : IProcess =
        let execute (specification: ProcessSpec) observe =
            let execution =
                flow {
                    let! cancellationToken = Flow.cancellationToken

                    let! outcome =
                        async {
                            return!
                                (task {
                                    use ioCancellation = new CancellationTokenSource()

                                    let token = ioCancellation.Token
                                    let commands = specification.Commands |> List.toArray
                                    let processes = ResizeArray<Diagnostics.Process>()
                                    let settled = Array.create specification.Commands.Length false
                                    let processIds = Array.create specification.Commands.Length -1
                                    let pidfds = Array.create specification.Commands.Length -1
                                    let stdinStops = Array.init specification.Commands.Length (fun _ -> new CancellationTokenSource())
                                    let stdinCompleted = Array.init specification.Commands.Length (fun _ -> TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously))
                                    let stdinStarted = Array.create specification.Commands.Length false
                                    let stdinOwned = Array.create specification.Commands.Length false
                                    let closeStdIns = Array.create specification.Commands.Length None
                                    let closeMainWindows = Array.create specification.Commands.Length None
                                    let stdoutCap, stderrCap = specification.MaxOutputBytes
                                    for command in commands do
                                        if stdoutCap.IsSome && inherited command.StdOut.Value then
                                            invalidArg "specification" "A stdout byte cap cannot be enforced on inherited stdout." // axial-allow-AXG003: task interop reports foreign failure by exception
                                        if stderrCap.IsSome && inherited command.StdErr.Value then
                                            invalidArg "specification" "A stderr byte cap cannot be enforced on inherited stderr." // axial-allow-AXG003: task interop reports foreign failure by exception
                                    let starts = ResizeArray<DateTimeOffset>()
                                    let elapsedStarts = ResizeArray<TimeSpan>()
                                    let exitTasks = ResizeArray<Task<unit>>()

                                    let launchComplete =
                                        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                                    use launchRegistration =
                                        token.Register(fun () -> launchComplete.TrySetCanceled(token) |> ignore)

                                    let elapsedEnds = Array.create commands.Length None
                                    let errorGate = obj ()
                                    let mutable firstFailure: exn option = None
                                    let files = Dictionary<string * bool, FileStream>()
                                    let outputGates = Array.init commands.Length (fun _ -> new SemaphoreSlim(1, 1))
                                    let errorGates = Array.init commands.Length (fun _ -> new SemaphoreSlim(1, 1))
                                    use observerGate = new SemaphoreSlim(1, 1)
                                    let stdoutTargets = commands |> Array.map (fun c -> instantiate c.StdOut.Value)
                                    let stderrTargets = commands |> Array.map (fun c -> instantiate c.StdErr.Value)

                                    let stdout =
                                        commands
                                        |> Array.mapi (fun i c ->
                                            let available =
                                                not (inherited c.StdOut.Value)
                                                && not (
                                                    specification.Connections
                                                    |> List.exists (fun (source, _, _) -> source = i)
                                                )

                                            new CaptureBuffer(
                                                (if available then
                                                     c.StdOutCapture.Value
                                                 else
                                                     OutputRetention.Nothing),
                                                available
                                            ))

                                    let stderr =
                                        commands
                                        |> Array.mapi (fun i c ->
                                            let available =
                                                not (inherited c.StdErr.Value)
                                                && not (
                                                    specification.Connections
                                                    |> List.exists (fun (source, _, both) -> source = i && both)
                                                )
                                                && not (specification.MergeStdErr && specification.Leaves.Contains i)

                                            new CaptureBuffer(
                                                (if available then
                                                     c.StdErrCapture.Value
                                                 else
                                                     OutputRetention.Nothing),
                                                available
                                            ))

                                    let startedAt = clock.UtcNow()
                                    let startedElapsed = clock.Elapsed()

                                    // Cancellation callbacks are notification plumbing. The work they wake is
                                    // supervised separately, so a throwing callback must not crash a pool thread.
                                    let cancelIgnoringCallbackErrors (source: CancellationTokenSource) =
                                        try
                                            source.Cancel()
                                        with _ ->
                                            ()

                                    let cancelOffThread (source: CancellationTokenSource) : Task =
                                        let completed =
                                            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                                        ThreadPool.QueueUserWorkItem(fun _ ->
                                            cancelIgnoringCallbackErrors source
                                            completed.TrySetResult() |> ignore)
                                        |> ignore

                                        completed.Task :> Task

                                    let observeFault (work: Task) =
                                        work.ContinueWith(
                                            (fun (completed: Task) ->
                                                // Reading Exception observes faults from detached user input.
                                                let _ = completed.Exception
                                                ()),
                                            CancellationToken.None,
                                            TaskContinuationOptions.OnlyOnFaulted
                                            ||| TaskContinuationOptions.ExecuteSynchronously,
                                            TaskScheduler.Default
                                        )
                                        |> ignore

                                    let closeStage stage request =
                                        async {
                                            try
                                                if stage < 0 || stage >= processIds.Length || processIds[stage] < 0 then
                                                    return Error (StopRequestError.Failed "The stage has not started.")
                                                elif lock settled (fun () -> settled[stage]) then
                                                    return Ok ()
                                                else
                                                    match request with
                                                    | StopRequest.CloseStdIn when stdinOwned[stage] ->
                                                        let shouldWait =
                                                            lock settled (fun () ->
                                                                not settled[stage] && stdinStarted[stage])

                                                        if not (lock settled (fun () -> settled[stage])) then
                                                            // Start the bound before requesting cancellation: user callbacks run
                                                            // outside `settled` and cannot delay the forced pipe close.
                                                            use closeTimer = new CancellationTokenSource()
                                                            let timer = clock.Sleep(closeStdInFeederBound, closeTimer.Token)
                                                            cancelOffThread stdinStops[stage] |> ignore

                                                            if shouldWait then
                                                                let! winner =
                                                                    Task.WhenAny(stdinCompleted[stage].Task, timer)
                                                                    |> Async.AwaitTask

                                                                if not (obj.ReferenceEquals(winner, timer)) then
                                                                    cancelIgnoringCallbackErrors closeTimer

                                                            closeStdIns[stage] |> Option.iter (fun close -> close ())
                                                            stdinCompleted[stage].TrySetResult() |> ignore

                                                        return Ok ()
                                                    | StopRequest.CloseStdIn -> return Error StopRequestError.Unsupported
                                                    | StopRequest.CloseMainWindow when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ->
                                                        let closed =
                                                            lock settled (fun () ->
                                                                if settled[stage] then Some true
                                                                else closeMainWindows[stage] |> Option.map (fun close -> close ()))
                                                        match closed with
                                                        | Some true -> return Ok ()
                                                        | _ -> return Error (StopRequestError.Failed "The process has no main window.")
                                                    | StopRequest.CloseMainWindow -> return Error StopRequestError.Unsupported
                                                    | StopRequest.Interrupt
                                                    | StopRequest.Terminate
                                                    | StopRequest.Hangup
                                                    | StopRequest.Signal _ when not (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ->
                                                        let signal =
                                                            match request with
                                                            | StopRequest.Interrupt -> 2
                                                            | StopRequest.Terminate -> 15
                                                            | StopRequest.Hangup -> 1
                                                            | StopRequest.Signal value -> value
                                                            | _ -> 0

                                                        let result =
                                                            lock settled (fun () ->
                                                                if settled[stage] then
                                                                    Ok ()
                                                                else
                                                                    let mutable sent = -1
                                                                    let mutable error = 38
                                                                    if pidfds[stage] >= 0 then
                                                                        try
                                                                            sent <- pidfd_send_signal(pidfds[stage], signal, 0n, 0u)
                                                                            if sent <> 0 then error <- Marshal.GetLastWin32Error()
                                                                        with :? EntryPointNotFoundException -> ()

                                                                    // The kill fallback by PID is not atomic with the settled check: on platforms
                                                                    // without pidfd (macOS, musl, glibc < 2.36, or any ENOSYS path) a child that
                                                                    // exits and has its PID reused between the check and the kill can be signalled
                                                                    // as an unrelated process. ESRCH is still treated as an already-exited success.
                                                                    if sent <> 0 && (pidfds[stage] < 0 || error = 38) then
                                                                        sent <- kill(processIds[stage], signal)
                                                                        if sent <> 0 then error <- Marshal.GetLastWin32Error()

                                                                    if sent = 0 || error = 3 then Ok ()
                                                                    else Error (StopRequestError.Failed(System.ComponentModel.Win32Exception(error).Message)))

                                                        return result
                                                    | _ ->
                                                        return Error StopRequestError.Unsupported
                                            with error ->
                                                return Error (StopRequestError.Failed error.Message)
                                        }

                                    let terminate () =
                                        for proc in lock processes (fun () -> processes.ToArray()) do
                                            try
                                                // Kill(true) must be attempted even after the direct child exits:
                                                // descendants may still own its redirected pipe. .NET cannot always
                                                // discover reparented descendants; closing our pipes below ensures
                                                // that an orphan cannot hold this execution's drain open.
#if NETSTANDARD2_1
                                                proc.Kill()
#else
                                                proc.Kill(entireProcessTree = true)
#endif
                                            with _ -> ()
                                            try
                                                if proc.StartInfo.RedirectStandardOutput then proc.StandardOutput.Close()
                                                if proc.StartInfo.RedirectStandardError then proc.StandardError.Close()
                                                if proc.StartInfo.RedirectStandardInput then proc.StandardInput.Close()
                                            with _ -> ()

                                    let mutable stopping = false
                                    let mutable hadFailureWhenStopping = false
                                    let mutable cancellationObserved = false
                                    let mutable stopLadderTask: Task<Exit<unit, Never>> option = None

                                    // The stop ladder runs at most once per execution. The first stop cause to be observed
                                    // under errorGate decides the final result: a stage timeout recorded before cancellation
                                    // is observed keeps StageTimedOut; a cancellation observed before any failure is recorded
                                    // produces Cause.Interrupt.
                                    // The ladder must not cancel I/O on its own thread: Cancel() runs the execution's
                                    // continuations inline, and they reach the cleanup below, which waits for this ladder
                                    // to finish. Queuing the cancellation lets the ladder complete first.
                                    let cancelIoOffLadder () =
                                        cancelOffThread ioCancellation |> ignore

                                    let runStopLadder =
                                        flow {
                                            for request, grace in specification.StopGracefully do
                                                let live =
                                                    lock settled (fun () ->
                                                        [ for stage in 0 .. processes.Count - 1 do
                                                              if not settled[stage] then yield stage ])

                                                let mutable sent = false

                                                for stage in live do
                                                    let! result = closeStage stage request |> Flow.fromAsync

                                                    match result with
                                                    | Ok () -> sent <- true
                                                    | Error StopRequestError.Unsupported -> ()
                                                    | Error (StopRequestError.Failed _) -> ()

                                                if sent && not live.IsEmpty && grace > TimeSpan.Zero then
                                                    // Wait on process exit rather than the monitor fiber: the timing-out
                                                    // stage can itself be the caller of this shared ladder.
                                                    let exits =
                                                        live
                                                        |> List.map (fun stage ->
                                                            Flow.fromTask (fun token -> task {
                                                                do! waitForExit processes[stage] token
                                                                return ()
                                                            }))
                                                        |> Flow.sequencePar
                                                        |> Flow.ignore

                                                    do! Flow.race exits (Flow.sleep grace)

                                            terminate ()
                                            cancelIoOffLadder ()
                                        }
                                        |> Flow.ensuring (
                                            Flow.delay (fun () ->
                                                terminate ()
                                                cancelIoOffLadder ()
                                                Flow.succeed ())
                                        )

                                    // Every stop cause shares one Flow execution. Memoizing at this bridge keeps
                                    // callbacks thin while the ladder itself uses Flow race/sleep semantics.
                                    let stopLadder =
                                        Lazy<Task<Exit<unit, Never>>>(
                                            (fun () -> runStopLadder |> Flow.startTask (ClockEnvironment clock)),
                                            LazyThreadSafetyMode.ExecutionAndPublication
                                        )

                                    let runStopLadderOnce () = stopLadder.Value

                                    let stopOnCancellation () =
                                        let hadFailure =
                                            lock errorGate (fun () ->
                                                if firstFailure.IsNone then cancellationObserved <- true
                                                firstFailure.IsSome)
                                        hadFailureWhenStopping <- hadFailure
                                        stopping <- true
                                        // Continue asynchronously: neither the Cancel() caller nor a pool worker is
                                        // occupied for the ladder's grace periods.
                                        stopLadderTask <- Some(runStopLadderOnce ())

                                    let recordFailure (error: exn) =
                                        let first =
                                            lock errorGate (fun () ->
                                                match firstFailure, error with
                                                | Some (:? StageExitException), (:? ProcessFailureException as failure) ->
                                                    match failure.Error with
                                                    | ProcessError.OutputTooLarge _ ->
                                                        firstFailure <- Some error
                                                        false
                                                    | _ -> false
                                                | Some _, _ -> false
                                                | None, _ ->
                                                    firstFailure <- Some error
                                                    true)

                                        if first then
                                            cancelIgnoringCallbackErrors ioCancellation
                                            terminate ()

                                    let byteCounts = Array.init commands.Length (fun _ -> Array.zeroCreate<int64> 2)
                                    let countBytes stage channel count =
                                        let index = if channel = OutputChannel.StdOut then 0 else 1
                                        let stdoutLimit, stderrLimit = specification.MaxOutputBytes
                                        let limit = if index = 0 then stdoutLimit else stderrLimit
                                        lock byteCounts[stage] (fun () ->
                                            let current = byteCounts[stage][index]
                                            match limit with
                                            | Some maximum when int64 count > maximum - current ->
                                                raise (ProcessFailureException(ProcessError.OutputTooLarge { // axial-allow-AXG003: task interop reports foreign failure by exception
                                                    Stage = stage
                                                    Channel = channel
                                                    Limit = maximum
                                                    Command = Configuration.renderCommand commands[stage]
                                                }))
                                            | _ -> byteCounts[stage][index] <- current + int64 count)

                                    // A continuation rather than `task { try return! work with error -> ... }`: the
                                    // F# compiler in SDK 10.0.4xx emits invalid IL for that generic shape here
                                    // (InvalidProgramException at run time).
                                    let supervise (work: Task<'value>) : Task<'value> =
                                        work
                                            .ContinueWith(
                                                (fun (completed: Task<'value>) ->
                                                    if
                                                        (completed.IsFaulted || completed.IsCanceled)
                                                        && not token.IsCancellationRequested
                                                    then
                                                        recordFailure (
                                                            if completed.IsFaulted then
                                                                completed.Exception.InnerException
                                                            else
                                                                TaskCanceledException(completed) :> exn
                                                        )

                                                    completed),
                                                CancellationToken.None,
                                                TaskContinuationOptions.ExecuteSynchronously,
                                                TaskScheduler.Default
                                            )
                                            .Unwrap()

                                    let monitor i (command: CommandDefinition) =
                                        (task {
                                            let wait = waitForExit processes[i] token

                                            match command.Timeout.Value with
                                            | Some after ->
                                                use timerCancellation =
                                                    CancellationTokenSource.CreateLinkedTokenSource(token)

                                                let remaining =
                                                    max TimeSpan.Zero (after - (clock.Elapsed() - elapsedStarts[i]))

                                                let timer = clock.Sleep(remaining, timerCancellation.Token)

                                                try
                                                    let! winner = Task.WhenAny(wait, timer)

                                                    if obj.ReferenceEquals(winner, timer) && not wait.IsCompleted then
                                                        do! timer

                                                        let timeoutFailure =
                                                            ProcessFailureException(
                                                                ProcessError.StageTimedOut {
                                                                    Stage = i
                                                                    Command = Configuration.renderCommand command
                                                                    Timeout = after
                                                                }
                                                            )

                                                        // Record the timeout only if cancellation has not already been
                                                        // observed: the first observed stop cause decides the final result.
                                                        lock errorGate (fun () ->
                                                            if not cancellationObserved && firstFailure.IsNone then
                                                                firstFailure <- Some timeoutFailure)
                                                        stopping <- true
                                                        let! _ = runStopLadderOnce ()
                                                        return raise timeoutFailure // axial-allow-AXG003: task interop reports foreign failure by exception

                                                    do! wait
                                                finally
                                                    timerCancellation.Cancel()
                                            | None -> do! wait

                                            elapsedEnds[i] <- Some(clock.Elapsed())
                                            lock settled (fun () ->
                                                settled[i] <- true
                                                if pidfds[i] >= 0 then
                                                    closeFd(pidfds[i]) |> ignore
                                                    pidfds[i] <- -1)

                                            if not (command.SuccessCodes.Value.Contains processes[i].ExitCode) then
                                                do! launchComplete.Task
                                                lock errorGate (fun () ->
                                                    if firstFailure.IsNone && not stopping then
                                                        firstFailure <- Some(StageExitException i))

                                            return ()
                                        })
                                        |> supervise

                                    let buildResult () =
                                        let final = Set.minElement specification.Leaves

                                        let stages =
                                            commands
                                            |> Array.mapi (fun i command ->
                                                let exitCode =
                                                    if i < processes.Count && processes[i].HasExited then
                                                        processes[i].ExitCode
                                                    else
                                                        -1

                                                {
                                                    Stage = i
                                                    ProcessId = (if i < processes.Count then processes[i].Id else -1)
                                                    Command = Configuration.renderCommand command
                                                    ExitCode = exitCode
                                                    Succeeded = command.SuccessCodes.Value.Contains exitCode
                                                    StartedAt = (if i < starts.Count then starts[i] else startedAt)
                                                    Duration =
                                                        (if i < elapsedStarts.Count then
                                                             (defaultArg elapsedEnds[i] (clock.Elapsed()))
                                                             - elapsedStarts[i]
                                                         else
                                                             TimeSpan.Zero)
                                                    StdOutCapture = stdout[i].Finish command.Encoding.Value
                                                    StdErrCapture = stderr[i].Finish command.Encoding.Value
                                                })
                                            |> Array.toList

                                        let contributors =
                                            stages
                                            |> List.filter (fun stage ->
                                                not (
                                                    specification.Connections
                                                    |> List.exists (fun (source, _, both) ->
                                                        source = stage.Stage && both)
                                                )
                                                && not (specification.MergeStdErr && stage.Stage = final))

                                        let full =
                                            contributors
                                            |> List.forall (fun stage ->
                                                commands[stage.Stage].StdErrCapture.Value = OutputRetention.All)

                                        let parts =
                                            contributors
                                            |> List.map (fun stage ->
                                                commands[stage.Stage].Encoding.Value, stage.StdErrCapture)

                                        let allBytes = parts |> List.sumBy (fun (_, capture) -> capture.Bytes.Length)
                                        let mutable skip = if full then 0 else max 0 (allBytes - 65536)

                                        let kept =
                                            parts
                                            |> List.map (fun (encoding, capture) ->
                                                let removed = min skip capture.Bytes.Length
                                                skip <- skip - removed

                                                let bytes =
                                                    if removed = capture.Bytes.Length then
                                                        [||]
                                                    else
                                                        capture.Bytes[removed..]

                                                encoding.GetString bytes, bytes)

                                        let errorCapture = {
                                            Text = kept |> List.map fst |> String.concat ""
                                            Bytes = kept |> List.map snd |> Array.concat
                                            Truncated =
                                                (not full && allBytes > 65536)
                                                || (contributors
                                                    |> List.exists (fun stage -> stage.StdErrCapture.Truncated))
                                            Available =
                                                contributors |> List.forall (fun stage -> stage.StdErrCapture.Available)
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

                                    try
                                        try
                                            Array.iter (openTargets fileSystem files) stdoutTargets
                                            Array.iter (openTargets fileSystem files) stderrTargets
                                            use registration = cancellationToken.Register stopOnCancellation

                                            for i, command in commands |> Array.indexed do
                                                token.ThrowIfCancellationRequested()

                                                let pipedOut =
                                                    specification.Connections
                                                    |> List.exists (fun (source, _, _) -> source = i)

                                                let pipedError =
                                                    specification.Connections
                                                    |> List.exists (fun (source, _, both) -> source = i && both)

                                                let isRoot =
                                                    specification.Connections
                                                    |> List.forall (fun (_, target, _) -> target <> i)

                                                let merged =
                                                    specification.MergeStdErr && specification.Leaves.Contains i

                                                let redirectInput =
                                                    not (
                                                        isRoot
                                                        && (match command.StdIn.Value with
                                                            | InputSource.Inherit -> true
                                                            | _ -> false)
                                                    )

                                                let redirectOutput = pipedOut || not (inherited stdoutTargets[i])

                                                let redirectError =
                                                    pipedError
                                                    || not (
                                                        inherited (
                                                            if merged then stdoutTargets[i] else stderrTargets[i]
                                                        )
                                                    )

                                                let proc =
                                                    try
                                                        new Diagnostics.Process(
                                                            StartInfo =
                                                                startInfo
                                                                    redirectInput
                                                                    redirectOutput
                                                                    redirectError
                                                                    command
                                                        )
                                                    with error ->
                                                        raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                            ProcessFailureException(
                                                                ProcessError.StartFailed {
                                                                    Command = Configuration.renderCommand command
                                                                    Message = error.Message
                                                                }
                                                            )
                                                        )

                                                try
                                                    if not (proc.Start()) then
                                                        raise (Exception (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Could not start {0}.", command.FileName))) // axial-allow-AXG003: task interop reports foreign failure by exception
                                                with error ->
                                                    proc.Dispose()

                                                    raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                        ProcessFailureException(
                                                            ProcessError.StartFailed {
                                                                Command = Configuration.renderCommand command
                                                                Message = error.Message
                                                            }
                                                        )
                                                    )

                                                lock processes (fun () -> processes.Add proc)
                                                processIds[i] <- proc.Id
                                                stdinOwned[i] <- redirectInput
                                                closeStdIns[i] <-
                                                    Some(fun () ->
                                                        // Dispose the pipe handle directly (not through StreamWriter.Close,
                                                        // which flushes first) so a feeder blocked on a full pipe is unblocked.
                                                        try proc.StandardInput.BaseStream.Dispose() with _ -> ()
                                                        try proc.StandardInput.Close() with _ -> ())
                                                closeMainWindows[i] <- Some(fun () -> proc.CloseMainWindow())
                                                if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                                                    try pidfds[i] <- pidfd_open(proc.Id, 0u)
                                                    with :? EntryPointNotFoundException -> pidfds[i] <- -1
                                                let stageStartedAt = clock.UtcNow()
                                                starts.Add(stageStartedAt)
                                                elapsedStarts.Add(clock.Elapsed())
                                                exitTasks.Add(monitor i command)

                                                match specification.OnStarted with
                                                | Some handler ->
                                                    let! handlerExit =
                                                        Async.StartAsTask(
                                                            handler.Invoke(i, proc.Id, Configuration.renderCommand command, stageStartedAt, closeStage i),
                                                            cancellationToken = token
                                                        )

                                                    match handlerExit with
                                                    | Exit.Success () -> ()
                                                    | Exit.Failure cause -> raiseStartedHandlerFailure token cause
                                                | None -> ()

                                            let copies =
                                                specification.Connections
                                                |> List.groupBy (fun (_, target, _) -> target)
                                                |> List.map (fun (target, connections) ->
                                                    lock settled (fun () -> stdinStarted[target] <- true)
                                                    (task {
                                                        use gate = new SemaphoreSlim(1, 1)
                                                        use pipelineCancellation =
                                                            CancellationTokenSource.CreateLinkedTokenSource(token, stdinStops[target].Token)
                                                        let pipelineToken = pipelineCancellation.Token

                                                        try
                                                            pipelineToken.ThrowIfCancellationRequested()
                                                            let write bytes =
                                                                (task {
                                                                    do! gate.WaitAsync pipelineToken

                                                                    try
                                                                        do!
                                                                            processes[target]
                                                                                .StandardInput.BaseStream
                                                                                .WriteAsync(bytes, 0, bytes.Length, pipelineToken)
                                                                    finally
                                                                        gate.Release() |> ignore
                                                                })

                                                            let pump source channel (stream: Stream) =
                                                                (task {
                                                                let buffer = Array.zeroCreate<byte> 4096
                                                                use pending = new MemoryStream()

                                                                let lineMode =
                                                                    connections.Length > 1
                                                                    && specification.Framing = OutputFraming.Lines

                                                                let encoding = commands[source].Encoding.Value
                                                                let decoder = encoding.GetDecoder()

                                                                let decoded =
                                                                    Array.zeroCreate<char>(encoding.GetMaxCharCount 1)

                                                                let singleByte = Array.zeroCreate<byte> 1
                                                                let mutable lineLength = 0
                                                                let mutable lastWasCR = false

                                                                let checkLength () =
                                                                    if
                                                                        lineLength > specification.MaxLineLength
                                                                        && not (
                                                                            lineLength =
                                                                                specification.MaxLineLength + 1
                                                                            && lastWasCR
                                                                        )
                                                                    then
                                                                        raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                                            ProcessFailureException(
                                                                                ProcessError.LineTooLong {
                                                                                    Stage = source
                                                                                    OriginalChannel = channel
                                                                                    Limit = specification.MaxLineLength
                                                                                }
                                                                            )
                                                                        )

                                                                let mutable reading = true

                                                                while reading do
                                                                    let! count =
                                                                        stream.ReadAsync(
                                                                            buffer,
                                                                            0,
                                                                            buffer.Length,
                                                                            token
                                                                        )

                                                                    if count > 0 then countBytes source channel count

                                                                    if count = 0 then
                                                                        reading <- false

                                                                        if lineMode then
                                                                            lineLength <-
                                                                                lineLength
                                                                                + decoder.GetChars(
                                                                                    [||],
                                                                                    0,
                                                                                    0,
                                                                                    decoded,
                                                                                    0,
                                                                                    true
                                                                                )

                                                                            if
                                                                                lineLength >
                                                                                    specification.MaxLineLength
                                                                            then
                                                                                raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                                                    ProcessFailureException(
                                                                                        ProcessError.LineTooLong {
                                                                                            Stage = source
                                                                                            OriginalChannel = channel
                                                                                            Limit =
                                                                                                specification
                                                                                                    .MaxLineLength
                                                                                        }
                                                                                    )
                                                                                )

                                                                        if pending.Length > 0 then
                                                                            do! write (pending.ToArray())
                                                                    elif lineMode then
                                                                        for b in buffer[.. count - 1] do
                                                                            pending.WriteByte b
                                                                            singleByte[0] <- b

                                                                            let length =
                                                                                decoder.GetChars(
                                                                                    singleByte,
                                                                                    0,
                                                                                    1,
                                                                                    decoded,
                                                                                    0,
                                                                                    false
                                                                                )

                                                                            for index in 0 .. length - 1 do
                                                                                if decoded[index] = '\n' then
                                                                                    do! write (pending.ToArray())
                                                                                    pending.SetLength 0L
                                                                                    lineLength <- 0
                                                                                    lastWasCR <- false
                                                                                else
                                                                                    lineLength <- lineLength + 1
                                                                                    lastWasCR <- decoded[index] = '\r'
                                                                                    checkLength ()
                                                                    else
                                                                        do! write buffer[.. count - 1]
                                                            })

                                                            let pumps = [|
                                                                for source, _, both in connections do
                                                                    yield
                                                                        pump
                                                                            source
                                                                            OutputChannel.StdOut
                                                                            processes[source].StandardOutput.BaseStream
                                                                        :> Task

                                                                    if both then
                                                                        yield
                                                                            pump
                                                                                source
                                                                                OutputChannel.StdErr
                                                                                processes[source].StandardError.BaseStream
                                                                            :> Task
                                                            |]

                                                            do! Task.WhenAll pumps
                                                        with
                                                        | :? OperationCanceledException when stdinStops[target].IsCancellationRequested -> ()
                                                        | error
                                                            when stdinStops[target].IsCancellationRequested
                                                                 && (error :? ObjectDisposedException
                                                                     || error :? IOException) ->
                                                            // Only pipe failures caused by the forced close are expected.
                                                            try processes[target].StandardInput.Close() with _ -> ()
                                                            stdinCompleted[target].TrySetResult() |> ignore
                                                        | error ->
                                                            try processes[target].StandardInput.Close() with _ -> ()
                                                            stdinCompleted[target].TrySetResult() |> ignore
                                                            return raise error // axial-allow-AXG003: task interop reports foreign failure by exception

                                                        try processes[target].StandardInput.Close() with _ -> ()
                                                        stdinCompleted[target].TrySetResult() |> ignore
                                                    })
                                                    |> supervise
                                                    :> Task)
                                                |> List.toArray

                                            let inputs = [|
                                                for i, command in commands |> Array.indexed do
                                                    if
                                                        specification.Connections
                                                        |> List.forall (fun (_, target, _) -> target <> i)
                                                    then
                                                        match command.StdIn.Value with
                                                        | InputSource.Inherit -> ()
                                                        | source ->
                                                            lock settled (fun () -> stdinStarted[i] <- true)
                                                            yield
                                                                ((task {
                                                                    let target = processes[i].StandardInput.BaseStream
                                                                    use inputCancellation =
                                                                        CancellationTokenSource.CreateLinkedTokenSource(token, stdinStops[i].Token)
                                                                    let inputToken = inputCancellation.Token

                                                                    try
                                                                        inputToken.ThrowIfCancellationRequested()
                                                                        match source with
                                                                        | InputSource.Empty
                                                                        | InputSource.Inherit -> ()
                                                                        | InputSource.Text text ->
                                                                            do!
                                                                                target.WriteAsync(
                                                                                    command.Encoding.Value.GetBytes text,
                                                                                    0,
                                                                                    command.Encoding.Value.GetByteCount text,
                                                                                    inputToken
                                                                                )
                                                                        | InputSource.Bytes bytes ->
                                                                            do!
                                                                                target.WriteAsync(
                                                                                    bytes,
                                                                                    0,
                                                                                    bytes.Length,
                                                                                    inputToken
                                                                                )
                                                                        | InputSource.File path ->
                                                                            use input =
                                                                                fileSystem.OpenFile(
                                                                                    path,
                                                                                    FileMode.Open,
                                                                                    FileAccess.Read,
                                                                                    FileShare.Read
                                                                                )

                                                                            do! input.CopyToAsync(target, 81920, inputToken)
                                                                        | InputSource.Stream source ->
                                                                            let streamCancellation =
                                                                                CancellationTokenSource.CreateLinkedTokenSource(inputToken)

                                                                            let write bytes =
                                                                                Flow.fromAsync (async {
                                                                                    let! cancellation = Async.CancellationToken
                                                                                    do! target.WriteAsync(bytes, 0, bytes.Length, cancellation) |> Async.AwaitTask
                                                                                })

                                                                            let work = source |> FlowStream.runForEachFlow write
                                                                            let pumping = runUserFlow streamCancellation.Token true work
                                                                            let exiting = exitTasks[i] :> Task
                                                                            let! winner = Task.WhenAny(pumping :> Task, exiting)

                                                                            if obj.ReferenceEquals(winner, exiting) then
                                                                                let cancellation = cancelOffThread streamCancellation
                                                                                use inputTimerCancellation = new CancellationTokenSource()
                                                                                let timer =
                                                                                    clock.Sleep(
                                                                                        closeStdInFeederBound,
                                                                                        inputTimerCancellation.Token
                                                                                    )
                                                                                let! stopped = Task.WhenAny(pumping :> Task, timer)

                                                                                if obj.ReferenceEquals(stopped, pumping) then
                                                                                    cancelIgnoringCallbackErrors inputTimerCancellation

                                                                                    try
                                                                                        do! pumping
                                                                                    with
                                                                                    | :? OperationCanceledException
                                                                                        when streamCancellation.IsCancellationRequested ->
                                                                                        ()
                                                                                else
                                                                                    // A source may ignore its token. Do not let it retain the
                                                                                    // completed process execution; observe any eventual fault.
                                                                                    observeFault pumping

                                                                                cancellation.ContinueWith(
                                                                                    (fun (_: Task) -> streamCancellation.Dispose()),
                                                                                    CancellationToken.None,
                                                                                    TaskContinuationOptions.ExecuteSynchronously,
                                                                                    TaskScheduler.Default
                                                                                )
                                                                                |> ignore
                                                                            else
                                                                                streamCancellation.Dispose()
                                                                                do! pumping

                                                                    with
                                                                    | :? OperationCanceledException when stdinStops[i].IsCancellationRequested -> ()
                                                                    | error
                                                                        when stdinStops[i].IsCancellationRequested
                                                                             && (error :? ObjectDisposedException
                                                                                 || error :? IOException) ->
                                                                        // Only pipe failures caused by the forced close are expected.
                                                                        try processes[i].StandardInput.Close() with _ -> ()
                                                                        stdinCompleted[i].TrySetResult() |> ignore
                                                                    | error ->
                                                                        try processes[i].StandardInput.Close() with _ -> ()
                                                                        stdinCompleted[i].TrySetResult() |> ignore
                                                                        return raise error // axial-allow-AXG003: task interop reports foreign failure by exception

                                                                    try processes[i].StandardInput.Close() with _ -> ()
                                                                    stdinCompleted[i].TrySetResult() |> ignore
                                                                 })
                                                                 |> supervise
                                                                :> Task)
                                            |]

                                            let read
                                                stage
                                                original
                                                observed
                                                target
                                                (capture: CaptureBuffer)
                                                (stream: Stream)
                                                =
                                                (task {
                                                    let buffer = Array.zeroCreate<byte> 4096
                                                    let encoding = commands[stage].Encoding.Value
                                                    let decoder = encoding.GetDecoder()

                                                    let chars =
                                                        Array.zeroCreate<char>(encoding.GetMaxCharCount buffer.Length)

                                                    let pending = StringBuilder()

                                                    let targetGate =
                                                        if observed = OutputChannel.StdOut then
                                                            outputGates[stage]
                                                        else
                                                            errorGates[stage]

                                                    let publish text =
                                                        (task {
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
                                                        })

                                                    let decode bytes count flush =
                                                        (task {
                                                            let length =
                                                                decoder.GetChars(bytes, 0, count, chars, 0, flush)

                                                            match specification.Framing with
                                                            | OutputFraming.Chunks ->
                                                                if length > 0 then
                                                                    do! publish (String(chars, 0, length))
                                                            | OutputFraming.Lines ->
                                                                for index in 0 .. length - 1 do
                                                                    let c = chars[index]

                                                                    if c = '\n' then
                                                                        if
                                                                            pending.Length > 0
                                                                            && pending[pending.Length - 1] = '\r'
                                                                        then
                                                                            pending.Length <- pending.Length - 1

                                                                        do! publish (pending.ToString())
                                                                        pending.Clear() |> ignore
                                                                    else
                                                                        pending.Append c |> ignore
                                                                        // One extra CR is permitted only while it may be a CRLF terminator.
                                                                        if
                                                                            pending.Length > specification.MaxLineLength
                                                                            && not (
                                                                                pending.Length =
                                                                                    specification.MaxLineLength + 1
                                                                                && c = '\r'
                                                                            )
                                                                        then
                                                                            raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                                                ProcessFailureException(
                                                                                    ProcessError.LineTooLong {
                                                                                        Stage = stage
                                                                                        OriginalChannel = original
                                                                                        Limit =
                                                                                            specification.MaxLineLength
                                                                                    }
                                                                                )
                                                                            )

                                                                if flush && pending.Length > 0 then
                                                                    if pending.Length > specification.MaxLineLength then
                                                                        raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                                            ProcessFailureException(
                                                                                ProcessError.LineTooLong {
                                                                                    Stage = stage
                                                                                    OriginalChannel = original
                                                                                    Limit = specification.MaxLineLength
                                                                                }
                                                                            )
                                                                        )

                                                                    do! publish (pending.ToString())
                                                                    pending.Clear() |> ignore
                                                        })

                                                    let mutable reading = true

                                                    while reading do
                                                        let! count = stream.ReadAsync(buffer, 0, buffer.Length, token)

                                                        if count > 0 then countBytes stage original count

                                                        if count = 0 then
                                                            reading <- false
                                                            do! decode [||] 0 true
                                                        else
                                                            let bytes = buffer[.. count - 1]
                                                            do! targetGate.WaitAsync token

                                                            try
                                                                capture.Append bytes

                                                                do!
                                                                    writeTarget
                                                                        console
                                                                        files
                                                                        token
                                                                        observed
                                                                        target
                                                                        bytes
                                                            finally
                                                                targetGate.Release() |> ignore

                                                            do! decode bytes count false
                                                })
                                                |> supervise

                                            let reads = ResizeArray<Task>()
                                            let final = Set.minElement specification.Leaves

                                            if not (inherited stdoutTargets[final]) then
                                                reads.Add(
                                                    read
                                                        final
                                                        OutputChannel.StdOut
                                                        OutputChannel.StdOut
                                                        stdoutTargets[final]
                                                        stdout[final]
                                                        processes[final].StandardOutput.BaseStream
                                                )

                                            for i in 0 .. commands.Length - 1 do
                                                if
                                                    not (
                                                        specification.Connections
                                                        |> List.exists (fun (source, _, both) -> source = i && both)
                                                    )
                                                then
                                                    let merged = specification.MergeStdErr && i = final
                                                    let target = if merged then stdoutTargets[i] else stderrTargets[i]

                                                    if not (inherited target) then
                                                        reads.Add(
                                                            read
                                                                i
                                                                OutputChannel.StdErr
                                                                (if merged then
                                                                     OutputChannel.StdOut
                                                                 else
                                                                     OutputChannel.StdErr)
                                                                target
                                                                (if merged then stdout[i] else stderr[i])
                                                                processes[i].StandardError.BaseStream
                                                        )

                                            launchComplete.TrySetResult() |> ignore
                                            let exits = exitTasks.ToArray()

                                            let io =
                                                (task {
                                                    do! Task.WhenAll(Array.concat [ copies; inputs; reads.ToArray() ])

                                                    for target in stdoutTargets do
                                                        do! finishTarget token target

                                                    for i in 0 .. stderrTargets.Length - 1 do
                                                        if not (specification.MergeStdErr && i = final) then
                                                            do! finishTarget token stderrTargets[i]
                                                })
                                                |> supervise

                                            let allExited = Task.WhenAll exits

                                            let drain =
                                                (task {
                                                    let! _ = allExited

                                                    match specification.DrainTimeout with
                                                    | Some after when not io.IsCompleted ->
                                                        use timerCancellation =
                                                            CancellationTokenSource.CreateLinkedTokenSource(token)

                                                        let allEnded = elapsedEnds |> Array.choose id |> Array.max

                                                        let remaining =
                                                            max TimeSpan.Zero (after - (clock.Elapsed() - allEnded))

                                                        let timer = clock.Sleep(remaining, timerCancellation.Token)

                                                        try
                                                            let! winner = Task.WhenAny(io :> Task, timer)

                                                            if
                                                                obj.ReferenceEquals(winner, timer) && not io.IsCompleted
                                                            then
                                                                do! timer

                                                                raise ( // axial-allow-AXG003: task interop reports foreign failure by exception
                                                                    IOException
                                                                        (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Process I/O did not finish within {0} after all process stages exited.", after))
                                                                )
                                                        finally
                                                            cancelIgnoringCallbackErrors timerCancellation
                                                    | _ -> ()
                                                })
                                                |> supervise

                                            do! Task.WhenAll [| io :> Task; drain :> Task |]

                                            match lock errorGate (fun () -> firstFailure) with
                                            | Some error -> return raise error // axial-allow-AXG003: task interop reports foreign failure by exception
                                            | None ->
                                                cancellationToken.ThrowIfCancellationRequested()
                                                return Choice1Of3(buildResult ())
                                        with error ->
                                            let original = lock errorGate (fun () -> defaultArg firstFailure error)
                                            cancelIgnoringCallbackErrors ioCancellation
                                            terminate ()

                                            // A cancellation callback may still own the shared ladder. Await it
                                            // asynchronously before process handles are disposed.
                                            match stopLadderTask with
                                            | Some ladder ->
                                                try
                                                    let! _ = ladder
                                                    ()
                                                with _ ->
                                                    ()
                                            | None -> ()

                                            for proc in processes do
                                                try
                                                    proc.WaitForExit()
                                                with _ ->
                                                    ()

                                            try
                                                let! _ = Task.WhenAll(exitTasks.ToArray())
                                                ()
                                            with _ ->
                                                ()

                                            match original with
                                            | _ when cancellationToken.IsCancellationRequested && not hadFailureWhenStopping ->
                                                return! Task.FromCanceled<_>(cancellationToken)
                                            | :? ObserverDefectException as defect ->
                                                return Choice3Of3 defect.InnerException
                                            | :? ProcessFailureException as failure -> return Choice2Of3 failure.Error
                                            | :? StageExitException as failure ->
                                                let result = buildResult ()

                                                return
                                                    Choice2Of3(
                                                        ProcessError.StageFailed {
                                                            Stage = result.Stages[failure.Stage]
                                                            Result = result
                                                        }
                                                    )
                                            | :? OperationCanceledException -> return Choice3Of3 original
                                            | _ ->
                                                return Choice2Of3(ProcessError.IoFailed { Message = original.Message })
                                    finally
                                        cancelIgnoringCallbackErrors ioCancellation
                                        terminate ()
                                        lock settled (fun () -> Array.fill settled 0 settled.Length true)
                                        for completion in stdinCompleted do completion.TrySetResult() |> ignore

                                        for proc in processes do
                                            try
                                                proc.WaitForExit()
                                            with _ ->
                                                ()

                                            proc.Dispose()

                                        for stop in stdinStops do
                                            stop.Dispose()

                                        for index in 0 .. pidfds.Length - 1 do
                                            if pidfds[index] >= 0 then
                                                closeFd(pidfds[index]) |> ignore
                                                pidfds[index] <- -1

                                        for file in files.Values do
                                            file.Dispose()

                                        for buffer in stdout do
                                            (buffer :> IDisposable).Dispose()

                                        for buffer in stderr do
                                            (buffer :> IDisposable).Dispose()

                                        for gate in outputGates do
                                            gate.Dispose()

                                        for gate in errorGates do
                                            gate.Dispose()
                                })
                                |> Async.AwaitTask
                        }

                    return!
                        match outcome with
                        | Choice1Of3 result -> Flow.succeed result
                        | Choice2Of3 error -> Flow.fail error
                        | Choice3Of3 defect -> Flow.die defect
                }

            let timed =
                match specification.OverallTimeout with
                | Some after ->
                    execution
                    |> Flow.timeout
                        after
                        (ProcessError.OverallTimedOut {
                            Specification =
                                (specification.Commands
                                 |> List.map Configuration.renderCommand
                                 |> String.concat " | ")
                            Timeout = after
                        })
                | None -> execution

            timed |> Flow.localEnv (fun () -> ClockEnvironment(clock))

        {
            new IProcess with
                member _.Execute(specification, observe) = execute specification observe
        }
#endif
