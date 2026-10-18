// axial-allow-effect-file: process
namespace Axial.Process

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Axial.Console
open Axial.FileSystem

#if !FABLE_COMPILER
module internal NativeProcess =
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

    let private runUserFlow token input (work: Flow<unit, string, unit>) =
        task {
            let! result = Async.StartAsTask(Flow.toAsync () work, cancellationToken = token)
            match result with
            | Exit.Success () -> ()
            | Exit.Failure cause ->
                let rec typed = function
                    | Cause.Fail _ -> true
                    | Cause.Then(left, right) | Cause.Both(left, right) -> typed left || typed right
                    | Cause.Traced(inner, _) -> typed inner
                    | _ -> false
                let rec defect = function
                    | Cause.Die error -> Some error
                    | Cause.Then(left, right) | Cause.Both(left, right) ->
                        match defect left with
                        | Some error -> Some error
                        | None -> defect right
                    | Cause.Traced(inner, _) -> defect inner
                    | _ -> None
                if typed cause then
                    raise (ProcessFailureException(
                        if input then ProcessError.InputFailed(cause.ToString())
                        else ProcessError.OutputFailed(cause.ToString())))
                else
                    match defect cause with
                    | Some error -> raise (ObserverDefectException error)
                    | None ->
                        token.ThrowIfCancellationRequested()
                        raise (ObserverDefectException(Exception(cause.ToString())))
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
                                    use ioCancellation =
                                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

                                    let token = ioCancellation.Token
                                    let commands = specification.Commands |> List.toArray
                                    let processes = ResizeArray<Diagnostics.Process>()
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

                                    let terminate () =
                                        for proc in lock processes (fun () -> processes.ToArray()) do
                                            try
                                                if not proc.HasExited then
#if NETSTANDARD2_1
                                                    proc.Kill()
#else
                                                    proc.Kill(entireProcessTree = true)
#endif
                                            with _ ->
                                                ()

                                    let recordFailure error =
                                        let first =
                                            lock errorGate (fun () ->
                                                if firstFailure.IsSome then
                                                    false
                                                else
                                                    firstFailure <- Some error
                                                    true)

                                        if first then
                                            ioCancellation.Cancel()
                                            terminate ()

                                    let supervise (work: Task<'value>) =
                                        (task {
                                            try
                                                return! work
                                            with error ->
                                                if not token.IsCancellationRequested then
                                                    recordFailure error

                                                return raise error // axial-allow-raise
                                        })

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

                                                        // axial-allow-raise
                                                        raise (
                                                            ProcessFailureException(
                                                                ProcessError.StageTimedOut {
                                                                    Stage = i
                                                                    Command = Configuration.renderCommand command
                                                                    Timeout = after
                                                                }
                                                            )
                                                        ) // axial-allow-raise

                                                    do! wait
                                                finally
                                                    timerCancellation.Cancel()
                                            | None -> do! wait

                                            elapsedEnds[i] <- Some(clock.Elapsed())

                                            if not (command.SuccessCodes.Value.Contains processes[i].ExitCode) then
                                                do! launchComplete.Task
                                                recordFailure (StageExitException i)

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
                                            use registration = token.Register terminate

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
                                                        // axial-allow-raise
                                                        raise (
                                                            ProcessFailureException(
                                                                ProcessError.StartFailed {
                                                                    Command = Configuration.renderCommand command
                                                                    Message = error.Message
                                                                }
                                                            )
                                                        ) // axial-allow-raise

                                                try
                                                    if not (proc.Start()) then
                                                        // axial-allow-raise
                                                        raise (Exception $"Could not start {command.FileName}.")
                                                with error ->
                                                    proc.Dispose()

                                                    // axial-allow-raise
                                                    raise (
                                                        ProcessFailureException(
                                                            ProcessError.StartFailed {
                                                                Command = Configuration.renderCommand command
                                                                Message = error.Message
                                                            }
                                                        )
                                                    ) // axial-allow-raise

                                                lock processes (fun () -> processes.Add proc)
                                                starts.Add(clock.UtcNow())
                                                elapsedStarts.Add(clock.Elapsed())
                                                exitTasks.Add(monitor i command)

                                            let copies =
                                                specification.Connections
                                                |> List.groupBy (fun (_, target, _) -> target)
                                                |> List.map (fun (target, connections) ->
                                                    (task {
                                                        use gate = new SemaphoreSlim(1, 1)

                                                        let write bytes =
                                                            (task {
                                                                do! gate.WaitAsync token

                                                                try
                                                                    do!
                                                                        processes[target]
                                                                            .StandardInput.BaseStream
                                                                            .WriteAsync(bytes, 0, bytes.Length, token)
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
                                                                        // axial-allow-raise
                                                                        raise (
                                                                            ProcessFailureException(
                                                                                ProcessError.LineTooLong {
                                                                                    Stage = source
                                                                                    OriginalChannel = channel
                                                                                    Limit = specification.MaxLineLength
                                                                                }
                                                                            )
                                                                        ) // axial-allow-raise

                                                                let mutable reading = true

                                                                while reading do
                                                                    let! count =
                                                                        stream.ReadAsync(
                                                                            buffer,
                                                                            0,
                                                                            buffer.Length,
                                                                            token
                                                                        )

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
                                                                                // axial-allow-raise
                                                                                raise (
                                                                                    ProcessFailureException(
                                                                                        ProcessError.LineTooLong {
                                                                                            Stage = source
                                                                                            OriginalChannel = channel
                                                                                            Limit =
                                                                                                specification
                                                                                                    .MaxLineLength
                                                                                        }
                                                                                    )
                                                                                ) // axial-allow-raise

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
                                                            |> supervise

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
                                                        processes[target].StandardInput.Close()
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
                                                            yield
                                                                ((task {
                                                                    let target = processes[i].StandardInput.BaseStream

                                                                    try
                                                                        match source with
                                                                        | InputSource.Empty
                                                                        | InputSource.Inherit -> ()
                                                                        | InputSource.Text text ->
                                                                            do!
                                                                                target.WriteAsync(
                                                                                    command.Encoding.Value.GetBytes text,
                                                                                    0,
                                                                                    command.Encoding.Value.GetByteCount text,
                                                                                    token
                                                                                )
                                                                        | InputSource.Bytes bytes ->
                                                                            do!
                                                                                target.WriteAsync(
                                                                                    bytes,
                                                                                    0,
                                                                                    bytes.Length,
                                                                                    token
                                                                                )
                                                                        | InputSource.File path ->
                                                                            use input =
                                                                                fileSystem.OpenFile(
                                                                                    path,
                                                                                    FileMode.Open,
                                                                                    FileAccess.Read,
                                                                                    FileShare.Read
                                                                                )

                                                                            do! input.CopyToAsync(target, 81920, token)
                                                                        | InputSource.Stream source ->
                                                                            use inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(token)
                                                                            let write bytes =
                                                                                Flow.fromAsync (async {
                                                                                    let! cancellation = Async.CancellationToken
                                                                                    do! target.WriteAsync(bytes, 0, bytes.Length, cancellation) |> Async.AwaitTask
                                                                                })
                                                                            let work = source |> FlowStream.runForEachFlow write
                                                                            let pumping = runUserFlow inputCancellation.Token true work
                                                                            let exiting = exitTasks[i] :> Task
                                                                            let! winner = Task.WhenAny(pumping :> Task, exiting)
                                                                            if obj.ReferenceEquals(winner, exiting) then
                                                                                inputCancellation.Cancel()
                                                                                try
                                                                                    do! pumping
                                                                                with :? OperationCanceledException when inputCancellation.IsCancellationRequested -> ()
                                                                            else
                                                                                do! pumping

                                                                    finally
                                                                        processes[i].StandardInput.Close()
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
                                                                            // axial-allow-raise
                                                                            raise (
                                                                                ProcessFailureException(
                                                                                    ProcessError.LineTooLong {
                                                                                        Stage = stage
                                                                                        OriginalChannel = original
                                                                                        Limit =
                                                                                            specification.MaxLineLength
                                                                                    }
                                                                                )
                                                                            ) // axial-allow-raise

                                                                if flush && pending.Length > 0 then
                                                                    if pending.Length > specification.MaxLineLength then
                                                                        // axial-allow-raise
                                                                        raise (
                                                                            ProcessFailureException(
                                                                                ProcessError.LineTooLong {
                                                                                    Stage = stage
                                                                                    OriginalChannel = original
                                                                                    Limit = specification.MaxLineLength
                                                                                }
                                                                            )
                                                                        ) // axial-allow-raise

                                                                    do! publish (pending.ToString())
                                                                    pending.Clear() |> ignore
                                                        })

                                                    let mutable reading = true

                                                    while reading do
                                                        let! count = stream.ReadAsync(buffer, 0, buffer.Length, token)

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

                                                                // axial-allow-raise
                                                                raise (
                                                                    IOException
                                                                        $"Process I/O did not finish within {after} after all process stages exited."
                                                                ) // axial-allow-raise
                                                        finally
                                                            timerCancellation.Cancel()
                                                    | _ -> ()
                                                })
                                                |> supervise

                                            do! Task.WhenAll [| io :> Task; drain :> Task |]

                                            match firstFailure with
                                            | Some error -> return raise error // axial-allow-raise
                                            | None -> return Choice1Of3(buildResult ())
                                        with error ->
                                            let original = lock errorGate (fun () -> defaultArg firstFailure error)
                                            ioCancellation.Cancel()
                                            terminate ()

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
                                            | :? OperationCanceledException when
                                                cancellationToken.IsCancellationRequested
                                                ->
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
                                        ioCancellation.Cancel()
                                        terminate ()

                                        for proc in processes do
                                            try
                                                proc.WaitForExit()
                                            with _ ->
                                                ()

                                            proc.Dispose()

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
