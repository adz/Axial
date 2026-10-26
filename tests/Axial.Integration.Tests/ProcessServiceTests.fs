namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Process
open Axial.Process.DSL
open Axial.PlatformService
open Axial.Console
open Axial.FileSystem
open Swensen.Unquote
open Xunit

type ProcessTestEnv =
    { Process: IProcess; Clock: IClock }
    interface IHasProcess with
        member this.Process = this.Process
    interface IHasClock with
        member this.Clock = this.Clock

module ProcessServiceTests =
    let private env () = { Process = Process.live Clock.live FileSystem.live Console.live; Clock = Clock.live }

    let private waitUntil timeout predicate =
        let deadline = DateTime.UtcNow + timeout
        while not (predicate ()) && DateTime.UtcNow < deadline do
            Thread.Sleep 20
        predicate ()

    let private processHasExited pid =
        try
            use nativeProcess = System.Diagnostics.Process.GetProcessById pid
            nativeProcess.HasExited
        with :? ArgumentException -> true

    let private run workflow =
        match Flow.runSync (env ()) workflow with
        | Exit.Success result -> result
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``flow stream input and flow callback transfer bytes in order`` () =
        let received = ResizeArray<byte>()
        let source =
            [ "one"; "two"; "three" ]
            |> List.map Encoding.UTF8.GetBytes
            |> FlowStream.fromSeq
        let result =
            cmd $"cat"
            |> stdinFrom (Input.flowStream source)
            |> stdoutTo (Output.callback (fun bytes -> Flow.fromAsync (async { received.AddRange bytes })))
            |> toFlow |> run
        test <@ result.StdOut = "onetwothree" @>
        test <@ Encoding.UTF8.GetString(received.ToArray()) = result.StdOut @>

    [<Fact>]
    let ``a disconnected stdin source lets the run return and reports its late defect`` () = task {
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let reported = TaskCompletionSource<exn>(TaskCreationOptions.RunContinuationsAsynchronously)
        let mutable feederCleanupSleeps = 0
        let clock =
            { new IClock with
                member _.UtcNow() = Clock.live.UtcNow()
                member _.Elapsed() = Clock.live.Elapsed()
                member _.Sleep(delay, cancellationToken) =
                    if delay = TimeSpan.FromMilliseconds 1.0 then
                        Interlocked.Increment(&feederCleanupSleeps) |> ignore
                    Clock.live.Sleep(delay, cancellationToken) }
        let testEnv = { Process = Process.live clock FileSystem.live Console.live; Clock = clock }
        let defect = InvalidOperationException "late stdin defect"
        let observer =
            { FiberObserver.none with
                OnUnobservedDefect = fun metadata error ->
                    if obj.ReferenceEquals(error, defect)
                       && (metadata |> Option.bind _.Name) = Some "flow-disconnect" then
                        reported.TrySetResult(error) |> ignore }
        let source =
            FlowStream.fromFlow (Flow.succeed (Encoding.UTF8.GetBytes "first\n"))
            |> FlowStream.append (
                Flow.fromTask (fun _ -> task {
                    entered.TrySetResult() |> ignore
                    do! release.Task
                    return raise defect
                })
                // The source ignores interruption; disconnect is the explicit choice not to wait for it.
                |> Flow.disconnect
                // disconnect needs a clock for its fiber; stdin sources run without an environment.
                |> Flow.localEnv (fun () -> ClockEnvironment(Clock.live))
                |> FlowStream.fromFlow)
        let processDone = TaskCompletionSource<Exit<ProcessResult, ProcessError>>(TaskCreationOptions.RunContinuationsAsynchronously)
        let running =
            shText "head -1"
            |> stdinFrom (Input.flowStream source)
            |> toFlow
            |> Flow.onExit (fun exit -> Flow.delay (fun () -> processDone.TrySetResult exit |> ignore; Flow.succeed ()))
            |> Flow.withFiberObserver observer
            |> fun workflow -> workflow.StartAsTask(testEnv)

        do! entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        let! outcome = processDone.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        match outcome with
        | Exit.Success result -> test <@ result.ExitCode = 0 @>
        | other -> failwithf "Expected process completion before the foreign source settled, got %A" other
        test <@ not reported.Task.IsCompleted @>
        test <@ feederCleanupSleeps = 0 @>
        // The disconnected source belongs to the root run, which waits for it.
        test <@ not running.IsCompleted @>

        release.TrySetResult() |> ignore
        let! observed = reported.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        test <@ obj.ReferenceEquals(observed, defect) @>
        let! _ = running.WaitAsync(TimeSpan.FromSeconds 3.0)
        ()
    }

    [<Fact>]
    let ``stdin interruption combined with a prompt finalizer defect fails the run`` () = task {
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let defect = InvalidOperationException "prompt stdin finalizer defect"
        let source =
            Flow.fromAsync (async {
                entered.TrySetResult() |> ignore
                do! Async.Sleep 30000
                return [||]
            })
            |> Flow.ensuring (Flow.die defect)
            |> FlowStream.fromFlow
        let running =
            shText "sleep 0.1"
            |> stdinFrom (Input.flowStream source)
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())

        do! entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
        match outcome with
        | Exit.Failure cause ->
            test <@ Cause.isInterrupted cause @>
            test <@ Cause.defects cause |> List.exists (fun error -> obj.ReferenceEquals(error, defect)) @>
        | other -> failwithf "Expected combined interruption and defect, got %A" other
    }

    [<Fact>]
    let ``a run waits for a slow stdin finalizer and fails with its defect`` () = task {
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalizing = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let defect = InvalidOperationException "late stdin finalizer defect"
        let finalizer =
            Flow.fromTask (fun _ -> task {
                finalizing.TrySetResult() |> ignore
                do! release.Task
            })
            |> Flow.bind (fun () -> Flow.die defect)
        let source =
            Flow.fromAsync (async {
                entered.TrySetResult() |> ignore
                do! Async.Sleep 30000
                return [||]
            })
            |> Flow.ensuring finalizer
            |> FlowStream.fromFlow
        let running =
            shText "sleep 0.1"
            |> stdinFrom (Input.flowStream source)
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())

        do! entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        // The child has exited and the feeder is in its finalizer: the run must still be waiting for it.
        do! finalizing.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        test <@ not running.IsCompleted @>

        release.TrySetResult() |> ignore
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
        match outcome with
        | Exit.Failure cause -> test <@ Cause.defects cause |> List.exists (fun error -> obj.ReferenceEquals(error, defect)) @>
        | other -> failwithf "Expected the finalizer defect, got %A" other
    }

    [<Fact>]
    let ``a run waits for a stdin source that ignores interruption and reports its defect`` () = task {
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let defect = InvalidOperationException "stubborn stdin defect"
        let source =
            FlowStream.fromFlow (Flow.succeed (Encoding.UTF8.GetBytes "first\n"))
            |> FlowStream.append (
                Flow.fromTask (fun _ -> task {
                    entered.TrySetResult() |> ignore
                    do! release.Task
                    return raise defect
                })
                |> FlowStream.fromFlow)
        let running =
            shText "head -1"
            |> stdinFrom (Input.flowStream source)
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())

        do! entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        // head has its line and exits; the source ignores interruption, so the run keeps waiting for it.
        test <@ not (running.Wait(TimeSpan.FromMilliseconds 300.0)) @>

        release.TrySetResult() |> ignore
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
        match outcome with
        | Exit.Failure cause -> test <@ Cause.defects cause |> List.exists (fun error -> obj.ReferenceEquals(error.GetBaseException(), defect)) @>
        | other -> failwithf "Expected the source defect, got %A" other
    }

    [<Fact>]
    let ``typed input failure is process error and defect remains defect`` () =
        let failing = FlowStream.fromFlow (Flow.fail "bad input" : Flow<unit, string, byte array>)
        let outcome = cmd $"cat" |> stdinFrom (Input.flowStream failing) |> toFlow |> Flow.runSync (env ())
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.InputFailed text)) -> test <@ text.Contains "bad input" @>
        | other -> failwithf "Expected input failure, got %A" other

        let broken = FlowStream.fromFlow (Flow.die (InvalidOperationException "broken input") : Flow<unit, string, byte array>)
        match cmd $"cat" |> stdinFrom (Input.flowStream broken) |> toFlow |> Flow.runSync (env ()) with
        | Exit.Failure(Cause.Die error) -> test <@ error.GetBaseException().Message = "broken input" @>
        | other -> failwithf "Expected defect, got %A" other

    [<Fact>]
    let ``input failure after a partial write remains typed when the child reads to EOF`` () = task {
        let failureReady = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let releaseFailure = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let failingTail =
            Flow.fromTask (fun token -> task {
                failureReady.TrySetResult() |> ignore
                do! releaseFailure.Task.WaitAsync(token)
                return Array.empty<byte>
            })
            |> Flow.bind (fun _ -> Flow.fail "input failed before EOF")
            |> FlowStream.fromFlow
        let source =
            FlowStream.fromFlow (Flow.succeed (Encoding.UTF8.GetBytes "partial input"))
            |> FlowStream.append failingTail
        let running =
            cmd $"cat"
            |> stdinFrom (Input.flowStream source)
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())

        do! failureReady.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        releaseFailure.TrySetResult() |> ignore
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.InputFailed text)) -> test <@ text.Contains "input failed before EOF" @>
        | other -> failwithf "Expected input failure, got %A" other
    }

    [<Fact>]
    let ``typed output failure is a process error`` () =
        let outcome =
            cmd $"printf sample"
            |> stdoutTo (Output.callback (fun _ -> Flow.fail "output refused"))
            |> toFlow |> Flow.runSync (env ())
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.OutputFailed cause)) -> test <@ cause.Contains "output refused" @>
        | other -> failwithf "Expected output failure, got %A" other

    [<Fact>]
    let ``plain command interruption routes a stop without an optional coordinator trigger`` () = task {
        use cancellation = new CancellationTokenSource()
        let pidPath = Path.GetTempFileName()
        try
            // Keep this a coordinator-less specification under a76da5b2b: one stage, empty stdin, no timeout,
            // observer, output policy, onStarted handler, or graceful-stop policy.
            let running =
                shText $"echo $$ > '{pidPath}'; exec sleep 30"
                |> toFlow
                |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            test <@ waitUntil (TimeSpan.FromSeconds 3.0) (fun () -> File.ReadAllText(pidPath).Trim() <> "") @>
            let childPid = File.ReadAllText(pidPath).Trim() |> Int32.Parse
            cancellation.Cancel()
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            test <@ outcome = Exit.Failure Cause.Interrupt @>
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> processHasExited childPid) @>
        finally
            cancellation.Cancel()
            File.Delete pidPath
    }

    [<Fact>]
    let ``single stage destination failures stop promptly and preserve the original error`` () = task {
        let cases =
            [ "callback", OutputTarget.Callback(fun _ -> Flow.fail "callback failed"), "callback failed"
              "sink write", OutputTarget.Sink((fun _ -> Flow.fail "sink write failed"), (fun () -> Flow.ok ())), "sink write failed"
              "file", Output.file "/dev/full", "No space left on device" ]
        for name, target, expected in cases do
            use rescue = new CancellationTokenSource()
            let producer =
                if name = "file" then shText "head -c 1048576 /dev/zero; exec sleep 30"
                else shText "printf ready; exec sleep 30"
            let running =
                producer
                |> stdoutTo target
                |> toFlow
                |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
            try
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
                match outcome with
                | Exit.Failure cause -> test <@ cause.ToString().Contains expected @>
                | other -> failwithf "Expected %s failure, got %A" name other
            finally
                rescue.Cancel()
    }

    [<Fact>]
    let ``single stage sink completion failure is returned promptly`` () = task {
        use rescue = new CancellationTokenSource()
        let running =
            cmd $"printf ready"
            |> stdoutTo (OutputTarget.Sink((fun _ -> Flow.ok ()), (fun () -> Flow.fail "sink completion failed")))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
        try
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure cause -> test <@ cause.ToString().Contains "sink completion failed" @>
            | other -> failwithf "Expected sink completion failure, got %A" other
        finally
            rescue.Cancel()
    }

    [<Fact>]
    let ``exiting child cancels infinite input and runs its finalizer`` () = task {
        let finalized = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source =
            FlowStream.fromFlow (Flow.succeed (Encoding.UTF8.GetBytes "first"))
            |> FlowStream.append (FlowStream.repeatFlow (
                Flow.fromAsync (async {
                    do! Async.Sleep 30000
                    return Encoding.UTF8.GetBytes "again"
                })
                |> Flow.ensuring (Flow.fromAsync (async { finalized.TrySetResult(()) |> ignore }))))
        let running = shText "head -c 1" |> stdinFrom (Input.flowStream source) |> toFlow |> fun work -> work.StartAsTask(env ())
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Success result -> test <@ result.StdOut = "f" @>
        | other -> failwithf "Expected success, got %A" other
        do! finalized.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
    }

    [<Fact>]
    let ``compound input failure remains typed`` () =
        let compound = Cause.Then(Cause.Interrupt, Cause.Fail(ProcessError.InputFailed "compound input"))
        // Exercise the settlement decision directly so process scheduling cannot hide the failure branch.
        test <@ ProcessRun.classifyInputExit (Exit.Failure compound) = Exit.Failure compound @>
        let source =
            Flow.ofExit (Exit.Failure (Cause.Then(Cause.Interrupt, Cause.Fail "compound input")))
            |> FlowStream.fromFlow
        match cmd $"cat" |> stdinFrom (Input.flowStream source) |> toFlow |> Flow.runSync (env ()) with
        | Exit.Failure(Cause.Fail(ProcessError.InputFailed text)) -> test <@ text.Contains "compound input" @>
        | other -> failwithf "Expected compound input failure, got %A" other

    [<Fact>]
    let ``typed input failure alongside a defect keeps the same defect`` () =
        let defect = InvalidOperationException "mixed input defect"
        let source =
            Flow.ofExit (Exit.Failure (Cause.Both(Cause.Fail "input rejected", Cause.Die defect)))
            |> FlowStream.fromFlow
        match cmd $"cat" |> stdinFrom (Input.flowStream source) |> toFlow |> Flow.runSync (env ()) with
        | Exit.Failure cause ->
            test <@ Cause.defects cause |> List.exists (fun error -> obj.ReferenceEquals(error, defect)) @>
        | other -> failwithf "Expected compound input failure, got %A" other

    [<Fact>]
    let ``input captures its environment before construction`` () =
        let source =
            Flow.envWith (fun (value: string) -> Encoding.UTF8.GetBytes value)
            |> Flow.localEnv (fun () -> "captured")
            |> FlowStream.fromFlow
        let result = cmd $"cat" |> stdinFrom (Input.flowStream source) |> toFlow |> run
        test <@ result.StdOut = "captured" @>

    [<Fact>]
    let ``interrupting suspended input closes its scope and kills the child`` () = task {
        use cancellation = new CancellationTokenSource()
        let entered = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalized = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source =
            flow {
                let! () = Flow.scopeFinalizer (Flow.fromAsync (async { finalized.TrySetResult(()) |> ignore }))
                let! () = Flow.fromAsync (async {
                    entered.TrySetResult(0) |> ignore
                    do! Async.Sleep 30000
                })
                return Encoding.UTF8.GetBytes "late"
            }
            |> FlowStream.fromFlow
        let running =
            shText "exec sleep 30"
            |> stdinFrom (Input.flowStream source)
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
        try
            let! _ = entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            cancellation.Cancel()
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure Cause.Interrupt -> ()
            | other -> failwithf "Expected interruption, got %A" other
            do! finalized.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        finally cancellation.Cancel()
    }

    [<Fact>]
    let ``live process timestamps transcripts through the supplied clock`` () =
        let fixedTime = DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero)
        let fixedEnv = { Process = Process.live (Clock.fromValue fixedTime) FileSystem.live Console.live; Clock = Clock.fromValue fixedTime }
        let workflow = cmd $"true" |> toFlow

        match Flow.runSync fixedEnv workflow with
        | Exit.Success result ->
            test <@ result.StartedAt = fixedTime @>
            test <@ result.Duration = TimeSpan.Zero @>
            test <@ result.Stages |> List.forall (fun stage -> stage.StartedAt = fixedTime && stage.Duration = TimeSpan.Zero) @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``arguments stay tokenized and pipelines connect stdout to stdin`` () =
        let value = "hello world"
        let workflow =
            cmd $"printf %%s {value}"
            => cmd $"tr '[:lower:]' '[:upper:]'"
            |> toFlow

        match Flow.runSync (env ()) workflow with
        | Exit.Success result ->
            test <@ result.StdOut = "HELLO WORLD" @>
            test <@ result.StdErr = "" @>
            test <@ result.ExitCodes = [ 0; 0 ] @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``partial pipeline startup terminates stages that already started`` () =
        let pidPath = Path.GetTempFileName()
        File.Delete pidPath
        let mutable leaked: System.Diagnostics.Process option = None
        try
            let first = Process.commandArgs "sh" [ "-c"; $"echo $$ > '{pidPath}'; sleep 30" ]
            let missing = Process.command $"axial-missing-{Guid.NewGuid():N}"
            let outcome = first => missing |> toFlow |> Flow.runSync (env ())

            match outcome with
            | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
                test <@ failure.Command.Contains "axial-missing-" @>
            | other -> failwithf "Expected startup failure, got %A" other

            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> File.Exists pidPath) @>
            let pid = File.ReadAllText(pidPath).Trim() |> Int32.Parse
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> processHasExited pid) @>
        finally
            leaked
            |> Option.iter (fun nativeProcess ->
                if not nativeProcess.HasExited then nativeProcess.Kill(entireProcessTree = true)
                nativeProcess.Dispose())
            if File.Exists pidPath then File.Delete pidPath

    [<Fact>]
    let ``configured timeout returns a process diagnostic and terminates the process`` () =
        let pidPath = Path.GetTempFileName()
        File.Delete pidPath
        let mutable timedOutProcess: System.Diagnostics.Process option = None
        try
            let timeout = TimeSpan.FromMilliseconds 250.0
            let workflow =
                shText $"echo $$ > '{pidPath}'; sleep 30"
                |> Process.timeout timeout
                |> Process.toFlow

            match Flow.runSync (env ()) workflow with
            | Exit.Failure(Cause.Fail(ProcessError.StageTimedOut failure)) ->
                test <@ failure.Command.Contains "sleep 30" @>
                test <@ failure.Timeout = timeout @>
            | other -> failwithf "Expected process timeout, got %A" other

            test <@ File.Exists pidPath @>
            let pid = File.ReadAllText(pidPath).Trim() |> Int32.Parse
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> processHasExited pid) @>
        finally
            timedOutProcess
            |> Option.iter (fun nativeProcess ->
                if not nativeProcess.HasExited then nativeProcess.Kill(entireProcessTree = true)
                nativeProcess.Dispose())
            if File.Exists pidPath then File.Delete pidPath

    [<Fact>]
    let ``caller cancellation terminates the complete process tree`` () =
        let childPidPath = Path.GetTempFileName()
        File.Delete childPidPath
        let mutable childProcess: System.Diagnostics.Process option = None
        use cancellation = new CancellationTokenSource()
        try
            let specification = shText $"sleep 30 & echo $! > '{childPidPath}'; wait"
            let running = Process.toFlow specification |> fun workflow -> workflow.StartAsTask((env ()), cancellation.Token)
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> File.Exists childPidPath) @>
            let childPid = File.ReadAllText(childPidPath).Trim() |> Int32.Parse
            let nativeChild = System.Diagnostics.Process.GetProcessById childPid
            childProcess <- Some nativeChild

            cancellation.Cancel()
            match running.GetAwaiter().GetResult() with
            | Exit.Failure Cause.Interrupt -> ()
            | other -> failwithf "Expected cancellation, got %A" other
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> nativeChild.HasExited) @>
        finally
            childProcess
            |> Option.iter (fun nativeProcess ->
                if not nativeProcess.HasExited then nativeProcess.Kill(entireProcessTree = true)
                nativeProcess.Dispose())
            if File.Exists childPidPath then File.Delete childPidPath

    [<Fact>]
    let ``working directory and environment overrides reach the native process`` () =
        let directory = Path.Combine(Path.GetTempPath(), $"axial-process-{Guid.NewGuid():N}")
        Directory.CreateDirectory directory |> ignore
        try
            let workingDirectoryResult =
                Process.commandArgs "sh" [ "-c"; "printf '%s|%s' \"$PWD\" \"$AXIAL_DEVICE\"" ]
                |> Process.workingDirectory directory
                |> Process.environment "AXIAL_DEVICE" "override"
                |> Process.toFlow
                |> run
            let environmentResult =
                Process.command $"/usr/bin/env"
                |> Process.environment "AXIAL_DEVICE" "override"
                |> Process.removeEnvironment "PATH"
                |> Process.toFlow
                |> run

            test <@ workingDirectoryResult.StdOut = $"{directory}|override" @>
            test <@ environmentResult.StdOut.Split('\n') |> Array.contains "AXIAL_DEVICE=override" @>
            test <@ environmentResult.StdOut.Split('\n') |> Array.exists (fun line -> line.StartsWith "PATH=") |> not @>
        finally
            Directory.Delete directory

    [<Fact>]
    let ``startup failure identifies the command without escaping the typed channel`` () =
        let executable = $"axial-missing-{Guid.NewGuid():N}"
        let outcome = Process.commandArgs executable [] |> Process.toFlow |> Flow.runSync (env ())
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
            test <@ failure.Command = executable @>
            test <@ String.IsNullOrWhiteSpace failure.Message = false @>
        | other -> failwithf "Expected startup failure, got %A" other

    [<Fact>]
    let ``streaming emits stdout and stderr before the complete result`` () =
        let stream =
            Process.commandArgs "sh" [ "-c"; "printf out; printf err >&2" ]
            |> Process.streamOutputs

        match stream |> ProcessStream.runCollect |> Flow.runSync (env ()) with
        | Exit.Success (outputs, ProcessCompletion.Completed result) ->
            test <@ outputs |> List.exists (fun output -> output.ObservedChannel = OutputChannel.StdOut && output.Text = "out") @>
            test <@ outputs |> List.exists (fun output -> output.ObservedChannel = OutputChannel.StdErr && output.Stage = 0 && output.Text = "err") @>
            test <@ result.StdOut = "out" && result.StdErr = "err" @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``run reports every non-zero stage through the typed error channel`` () =
        let workflow =
            Process.commandArgs "sh" [ "-c"; "exit 7" ]
            => Process.command $"cat"
            |> toFlow

        match Flow.runSync (env ()) workflow with
        | Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
            test <@ failure.Stage.Stage = 0 @>
            // A failing stage terminates the rest of the pipeline, so `cat` either reached end of input first (0)
            // or was killed (137); which one is a race.
            test <@ List.head failure.Result.ExitCodes = 7 @>
            test <@ List.contains (List.item 1 failure.Result.ExitCodes) [ 0; 137 ] @>
        | result -> failwithf "Expected a typed non-zero exit, got %A" result

    [<Fact>]
    let ``tail capture is bounded and reports truncation`` () =
        let result =
            cmd $"printf %%s 0123456789"
            |> Process.captureTail 4
            |> Process.toFlow
            |> run

        test <@ result.StdOut = "6789" @>
        test <@ result.StdOutCapture.Truncated @>
        test <@ result.StdOutCapture.Bytes.Length = 4 @>

    [<Fact>]
    let ``tee writes exact bytes to a file and retains a bounded tail`` () =
        let path = Path.GetTempFileName()
        try
            let result =
                cmd $"printf %%s artifact-output"
                |> Process.stdoutToFile path
                |> captureTail 6
                |> Process.toFlow
                |> run

            test <@ File.ReadAllText path = "artifact-output" @>
            test <@ result.StdOut = "output" @>
            test <@ result.StdOutCapture.Truncated @>
        finally File.Delete path

    [<Fact>]
    let ``binary output is retained exactly and decoded with the configured encoding`` () =
        let result =
            Process.commandArgs "sh" [ "-c"; "printf '\\377\\000A'" ]
            |> Process.encoding Encoding.Latin1
            |> Process.toFlow
            |> run

        test <@ result.StdOutCapture.Bytes = [| 255uy; 0uy; 65uy |] @>
        test <@ result.StdOut.Length = 3 @>

    [<Fact>]
    let ``accepted exit codes are immutable per command`` () =
        let result =
            Process.commandArgs "sh" [ "-c"; "exit 7" ]
            |> Process.successCodes [ 0; 7 ]
            |> Process.toFlow
            |> run

        test <@ result.Stages.Head.ExitCode = 7 @>
        test <@ result.Stages.Head.Succeeded @>

    [<Fact>]
    let ``rendering and transcripts redact secret arguments`` () =
        let token = secret "super-secret-token"
        let command = cmd $"printf %%s {token}"

        let rendered = Process.render command
        test <@ rendered = "printf \"%s\" ***" @>
        let plan = command |> Process.plan
        test <@ plan.Commands = [ "printf \"%s\" ***" ] @>
        test <@ Process.renderPlan plan = [ "commands: printf \"%s\" ***"; "stage 0: in empty, out discard, err discard" ] @>
        let result = command |> toFlow |> run
        test <@ result.Stages.Head.Command = "printf \"%s\" ***" @>
        test <@ result.Stages.Head.Command.Contains("super-secret-token") = false @>

    [<Fact>]
    let ``native process stream emits output and a final transcript`` () =
        let stream =
            cmd $"printf %%s streamed"
            |> Process.framing OutputFraming.Lines
            |> Process.streamOutputs

        let events = stream |> ProcessStream.runCollect |> Flow.runSync (env ())
        match events with
        | Exit.Success ([output], ProcessCompletion.Completed result) ->
            test <@ output.Text = "streamed" @>
            test <@ result.StdOut = "streamed" @>
            test <@ result.Duration >= TimeSpan.Zero @>
        | other -> failwithf "Unexpected stream events: %A" other

    [<Fact>]
    let ``ending stream consumption terminates the native process tree`` () =
        let pidPath = Path.GetTempFileName()
        File.Delete pidPath
        try
            let events =
                shText $"echo $$ > '{pidPath}'; printf 'ready\n'; sleep 30"
                |> Process.framing OutputFraming.Lines
                |> Process.streamOutputs
                |> ProcessStream.take 1
                |> ProcessStream.runCollect
                |> Flow.runSync (env ())

            match events with
            | Exit.Success ([output], ProcessCompletion.StoppedEarly) -> test <@ output.Text = "ready" @>
            | other -> failwithf "Expected one output event, got %A" other
            test <@ File.Exists pidPath @>
            let pid = File.ReadAllText(pidPath).Trim() |> Int32.Parse
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> processHasExited pid) @>
        finally
            if File.Exists pidPath then File.Delete pidPath

    [<Fact>]
    let ``collection pipelines use implicit yields and endpoint composition`` () =
        let result =
            pipe [
                $"printf %%s cba"
                $"rev"
                $"tr '[:lower:]' '[:upper:]'"
            ]
            |> toFlow
            |> run

        test <@ result.StdOut = "ABC" @>
        test <@ result.ExitCodes = [ 0; 0; 0 ] @>

    [<Fact>]
    let ``text bytes and files are first class input endpoints`` () =
        let path = Path.GetTempFileName()
        try
            File.WriteAllText(path, "from-file")
            let text = Input.text "from-text" => cmd $"cat" |> toFlow |> run
            let bytes = Input.bytes [| 0uy; 1uy; 255uy |] => cmd $"cat" |> toFlow |> run
            let file = Input.file path => cmd $"cat" |> toFlow |> run
            test <@ text.StdOut = "from-text" @>
            test <@ bytes.StdOutCapture.Bytes = [| 0uy; 1uy; 255uy |] @>
            test <@ file.StdOut = "from-file" @>
        finally File.Delete path

    [<Fact>]
    let ``large input and output are pumped concurrently without deadlock`` () =
        let payload = Array.init (1024 * 1024) (fun index -> byte (index % 251))
        let result = Input.bytes payload => cmd $"cat" |> toFlow |> run
        test <@ result.StdOutCapture.Bytes = payload @>

    [<Fact>]
    let ``output file endpoint configures a command before explicit execution`` () =
        let path = Path.GetTempFileName()
        try
            let result = cmd $"printf %%s written" |> captureNothing => Output.file path |> toFlow |> run
            test <@ File.ReadAllText path = "written" @>
            test <@ result.StdOut = "" @>
        finally File.Delete path

    [<Fact>]
    let ``stream adapters move exact bytes incrementally`` () =
        let payload = [| 0uy; 7uy; 255uy; 10uy |]
        use source = new MemoryStream(payload)
        use target = new MemoryStream()
        let result = Input.stream source => (cmd $"cat" |> captureNothing) => Output.stream target |> toFlow |> run
        test <@ target.ToArray() = payload @>
        test <@ result.StdOut = "" @>

    [<Fact>]
    let ``true inherited handles execute without redirected capture`` () =
        let result =
            cmd $"true"
            |> Process.stdoutTo Output.inheritHandles
            |> Process.stderrTo Output.inheritHandles
            |> Process.toFlow
            |> run
        test <@ result.ExitCode = 0 @>
        test <@ result.StdOut = "" @>

    [<Fact>]
    let ``inherited stdin gives the root process the host stdin handle`` () =
        let hostStdin = IO.FileInfo("/proc/self/fd/0").LinkTarget
        let inherited =
            shText "readlink /proc/self/fd/0"
            |> Process.stdinFrom Input.inheritHandles
            |> Process.toFlow
            |> run
        let redirected = shText "readlink /proc/self/fd/0" |> toFlow |> run
        test <@ inherited.StdOut.Trim() = hostStdin @>
        test <@ redirected.StdOut.Trim() <> hostStdin @>

    [<Fact>]
    let ``merge stderr routes final stderr through stdout targets`` () =
        let result =
            shText "printf out; printf err >&2"
            |> mergeStderr
            |> toFlow
            |> run

        test <@ result.StdOut.Contains "out" @>
        test <@ result.StdOut.Contains "err" @>
        test <@ result.StdErr = "" @>

    [<Fact>]
    let ``pipe both connects stdout and stderr to the next command`` () =
        let result =
            shText "printf out; printf err >&2"
            |> pipeStdoutAndStderrTo (cmd $"cat")
            |> toFlow
            |> run

        test <@ result.StdOut.Contains "out" @>
        test <@ result.StdOut.Contains "err" @>

    [<Fact>]
    let ``shell interpolation is passed out of band and cannot inject syntax`` () =
        let value = "safe; printf injected"
        let command = bash $"printf %%s {value}"
        let result = command |> toFlow |> run
        test <@ result.StdOut = value @>
        let rendered = Process.render command
        test <@ rendered.Contains value @>

    [<Fact>]
    let ``parallel capture preserves command order`` () =
        let results =
            [ cmd $"printf %%s first"; cmd $"printf %%s second"; cmd $"printf %%s third" ]
            |> captureParallel 2
            |> run

        test <@ results |> List.map _.StdOut = [ "first"; "second"; "third" ] @>

    [<Fact>]
    let ``line framed fan in connects concurrent producers to one consumer`` () =
        let result =
            mergeStdout [ shText "printf 'alpha\\n'"; shText "printf 'beta\\n'" ]
            => cmd $"sort"
            |> toFlow
            |> run

        test <@ result.StdOut = "alpha\nbeta\n" @>
        test <@ result.ExitCodes = [ 0; 0; 0 ] @>

    [<Fact>]
    let ``true inheritance cannot be combined with tee`` () =
        raises<ArgumentException> <@
            cmd $"printf inherited"
            |> Process.stdoutTo (OutputTarget.Tee [ Output.inheritHandles; Output.discard ])
            |> ignore @>

    [<Fact>]
    let ``failed output callback cancels other pumps and preserves the original failure`` () = task {
        use rescue = new CancellationTokenSource()
        let pid = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
        let callback (bytes: byte array) = async {
            pid.TrySetResult(Encoding.UTF8.GetString(bytes) |> Int32.Parse) |> ignore
            return raise (IOException "output destination failed")
        }
        let running =
            shText "printf '%s' $$; exec sleep 30"
            |> stdoutTo (Output.callback (fun bytes -> Flow.fromAsync (callback bytes)))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
        try
            let! childPid = pid.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure(Cause.Die failure) ->
                test <@ failure.GetBaseException().Message = "output destination failed" @>
            | other -> failwithf "Expected destination failure, got %A" other
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> processHasExited childPid) @>
        finally
            rescue.Cancel()
    }

    [<Fact>]
    let ``caller cancellation reaches an asynchronous output callback`` () = task {
        use cancellation = new CancellationTokenSource()
        let entered = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
        let stopped = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let callback (bytes: byte array) = async {
            entered.TrySetResult(Encoding.UTF8.GetString(bytes) |> Int32.Parse) |> ignore
            try do! Async.Sleep 30000
            finally stopped.TrySetResult(()) |> ignore
        }
        let running =
            shText "printf '%s' $$; exec sleep 30"
            |> stdoutTo (Output.callback (fun bytes -> Flow.fromAsync (callback bytes)))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
        try
            let! childPid = entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            cancellation.Cancel()
            do! stopped.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure Cause.Interrupt -> ()
            | other -> failwithf "Expected interruption, got %A" other
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> processHasExited childPid) @>
        finally
            cancellation.Cancel()
    }

    [<Fact>]
    let ``early consumer exit stops an unbounded producer`` () = task {
        use rescue = new CancellationTokenSource()
        let running =
            shText "while :; do printf 'abcdefghijklmnopqrstuvwxyz'; done"
            => cmd $"head -c 1"
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
        try
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure(Cause.Fail(ProcessError.IoFailed _)) -> ()
            | other -> failwithf "Expected broken-pipe failure, got %A" other
        finally
            rescue.Cancel()
    }

    [<Fact>]
    let ``foreign callback cancellation is a defect rather than caller interruption`` () = task {
        use rescue = new CancellationTokenSource()
        let running =
            shText "printf ready; exec sleep 30"
            |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { return raise (OperationCanceledException "foreign cancellation") })))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
        try
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure(Cause.Die _) -> ()
            | other -> failwithf "Expected foreign cancellation defect, got %A" other
        finally
            rescue.Cancel()
    }

    [<Fact>]
    let ``stream adapters pass process cancellation to native stream operations`` () = task {
        for operation in [ "input"; "output"; "flush" ] do
            use cancellation = new CancellationTokenSource()
            let entered = TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously)
            let stopped = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let block token = task {
                entered.TrySetResult token |> ignore
                try
                    let! _ = Task.WhenAny(release.Task, Task.Delay(Timeout.Infinite, token))
                    token.ThrowIfCancellationRequested()
                finally stopped.TrySetResult(()) |> ignore
            }
            use target =
                { new MemoryStream() with
                    override _.ReadAsync(_, _, _, token) = task {
                        do! block token
                        return 0
                    }
                    override _.WriteAsync(_, _, _, token) =
                        if operation = "output" then block token :> Task else Task.CompletedTask
                    override _.FlushAsync(token) = block token :> Task }
            let specification =
                match operation with
                | "input" -> shText "exec sleep 30" |> stdinFrom (Input.stream target)
                | "output" -> shText "printf ready; exec sleep 30" |> stdoutTo (Output.stream target)
                | _ -> cmd $"printf ready" |> stdoutTo (Output.stream target)
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            try
                let! token = entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                let canBeCanceled = token.CanBeCanceled
                test <@ canBeCanceled @>
                cancellation.Cancel()
                do! stopped.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
                match outcome with
                | Exit.Failure Cause.Interrupt -> ()
                | other -> failwithf "Expected %s interruption, got %A" operation other
            finally
                cancellation.Cancel()
                release.TrySetResult(()) |> ignore
    }

    [<Fact>]
    let ``drain timeout reports inherited output pipes without claiming successful capture`` () = task {
        let childPidPath = Path.GetTempFileName()
        use rescue = new CancellationTokenSource()
        try
            let specification =
                shText $"sleep 30 & echo $! > '{childPidPath}'; printf ready"
                |> drainTimeout (TimeSpan.FromMilliseconds 100.0)
            let plan = specification |> Process.plan
            test <@ plan.DrainTimeout = Some(TimeSpan.FromMilliseconds 100.0) @>
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure(Cause.Fail(ProcessError.IoFailed failure)) ->
                test <@ failure.Message.Contains "after all process stages exited" @>
            | other -> failwithf "Expected drain failure, got %A" other
        finally
            rescue.Cancel()
            let text = File.ReadAllText(childPidPath).Trim()
            if text <> "" then
                let childPid = Int32.Parse text
                try
                    use child = System.Diagnostics.Process.GetProcessById childPid
                    if not child.HasExited then child.Kill()
                with :? ArgumentException -> ()
            File.Delete childPidPath
    }

    [<Fact>]
    let ``default draining preserves output written by descendants after their parent exits`` () =
        let result =
            shText "(sleep 0.1; printf late) & printf early"
            |> toFlow
            |> run
        test <@ result.StdOut = "earlylate" @>
        let plan = cmd $"true" |> Process.plan
        test <@ plan.DrainTimeout = None @>

    [<Fact>]
    let ``drain timeout rejects nonpositive durations`` () =
        for duration in [ TimeSpan.Zero; TimeSpan.FromSeconds -1.0 ] do
            Assert.Throws<ArgumentException>(fun () -> cmd $"true" |> drainTimeout duration |> ignore) |> ignore

    [<Fact>]
    let ``process stream reports a failed destination instead of waiting for another event`` () = task {
        use rescue = new CancellationTokenSource()
        let running =
            shText "printf ready; exec sleep 30"
            |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { return raise (IOException "stream destination failed") })))
            |> streamOutputs
            |> ProcessStream.runCollect
            |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
        try
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure(Cause.Die failure) ->
                test <@ failure.GetBaseException().Message = "stream destination failed" @>
            | other -> failwithf "Expected stream failure, got %A" other
        finally
            rescue.Cancel()
    }

    [<Fact>]
    let ``drain timeout also cancels sink completion after process exit`` () = task {
        use rescue = new CancellationTokenSource()
        let stopped = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finishing = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let sleeping = TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously)
        let advance = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let clock =
            { new IClock with
                member _.UtcNow() = DateTimeOffset.UnixEpoch
                member _.Elapsed() = TimeSpan.Zero
                member _.Sleep(delay, token) =
                    sleeping.TrySetResult delay |> ignore
                    advance.Task.WaitAsync(token) :> Task }
        let host = { Process = Process.live clock FileSystem.live Console.live; Clock = clock }
        let finish () = async {
            finishing.TrySetResult(()) |> ignore
            try do! Async.Sleep 30000
            finally stopped.TrySetResult(()) |> ignore
        }
        let running =
            cmd $"printf ready"
            |> stdoutTo (OutputTarget.Sink((fun _ -> Flow.ok ()), (fun () -> Flow.fromAsync (finish ()))))
            |> drainTimeout (TimeSpan.FromMilliseconds 100.0)
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(host, rescue.Token)
        try
            let! requestedDelay = sleeping.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            test <@ requestedDelay = TimeSpan.FromMilliseconds 100.0 @>
            test <@ not running.IsCompleted @>
            do! finishing.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            advance.TrySetResult(()) |> ignore
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match outcome with
            | Exit.Failure(Cause.Fail(ProcessError.IoFailed failure)) ->
                test <@ failure.Message.Contains "after all process stages exited" @>
            | other -> failwithf "Expected sink drain failure, got %A" other
            do! stopped.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        finally
            rescue.Cancel()
    }

    [<Fact>]
    let ``onStarted reports pid before output and terminate reaches the stage`` () = task {
        let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
        let output = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let events = ConcurrentQueue<string>()
        let specification =
            shText "trap 'echo term; exit 3' TERM; echo ready; while :; do sleep 1; done"
            |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async {
                events.Enqueue "output"
                output.TrySetResult() |> ignore
            })))
            |> Process.successCodes (set [ 3 ])
            |> onStarted (fun handle -> Flow.fromAsync (async {
                events.Enqueue "started"
                started.TrySetResult handle |> ignore
            }))
        let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env ())
        let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        test <@ File.Exists("/proc/" + string handle.ProcessId + "/status") @>
        do! output.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        test <@ events.ToArray()[..1] = [| "started"; "output" |] @>
        let! closeResult = handle.Close StopRequest.Terminate |> fun workflow -> workflow.StartAsTask(())
        test <@ closeResult = Exit.Success () @>
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Success result ->
            test <@ result.Stages[0].ProcessId = handle.ProcessId @>
            test <@ result.ExitCode = 3 @>
            test <@ result.StdOut.Contains "term" @>
        | other -> failwithf "Expected successful handled exit, got %A" other
    }

    [<Fact>]
    let ``onStarted preserves handler defects and caller interruption`` () = task {
        let defect = InvalidOperationException "started handler failed"
        let defectOutcome =
            cmd $"true"
            |> onStarted (fun _ -> Flow.die defect)
            |> toFlow
            |> Flow.runSync (env ())
        match defectOutcome with
        | Exit.Failure(Cause.Die error) -> test <@ obj.ReferenceEquals(error, defect) @>
        | other -> failwithf "Expected original handler defect, got %A" other

        use cancellation = new CancellationTokenSource()
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let running =
            shText "exec sleep 30"
            |> onStarted (fun _ -> Flow.fromAsync (async {
                entered.TrySetResult() |> ignore
                do! Async.Sleep 30000
            }))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
        do! entered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        cancellation.Cancel()
        let! interrupted = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        test <@ interrupted = Exit.Failure Cause.Interrupt @>
    }

    [<Fact>]
    let ``CloseStdIn lets a waiting process finish`` () = task {
        let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
        let running =
            cmd $"cat"
            |> stdinFrom (Input.flowStream (FlowStream.repeatFlow (Flow.fromAsync (async {
                do! Async.Sleep 30000
                return [||]
            }))))
            |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())
        let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        let! closeResult = handle.Close StopRequest.CloseStdIn |> fun workflow -> workflow.StartAsTask(())
        test <@ closeResult = Exit.Success () @>
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Success result -> test <@ result.ExitCode = 0 @>
        | other -> failwithf "Expected cat to exit, got %A" other
    }

    [<Fact>]
    let ``CloseMainWindow is unsupported on Unix`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            use rescue = new CancellationTokenSource()
            let running =
                shText "exec sleep 30"
                |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
                |> toFlow
                |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            let! outcome = handle.Close StopRequest.CloseMainWindow |> fun workflow -> workflow.StartAsTask(())
            match outcome with
            | Exit.Failure(Cause.Fail(ProcessError.StopUnsupported StopRequest.CloseMainWindow)) -> ()
            | other -> failwithf "Expected unsupported stop request, got %A" other
            rescue.Cancel()
            let! _ = running
            ()
    }

    [<Fact>]
    let ``stopGracefully sends TERM before interruption kills the tree`` () = task {
        let marker = Path.Combine(Path.GetTempPath(), "axial-graceful-" + Guid.NewGuid().ToString("N"))
        use cancellation = new CancellationTokenSource()
        let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let specification =
            shText ("trap 'echo stopped > " + marker + "; exit 0' TERM; echo ready; while :; do sleep 1; done")
            |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
            |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 2.0 ]
        let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
        try
            do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! Task.Run(fun () -> cancellation.Cancel()).WaitAsync(TimeSpan.FromSeconds 5.0)
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
            match outcome with
            | Exit.Failure Cause.Interrupt -> test <@ File.Exists marker @>
            | other -> failwithf "Expected interruption, got %A" other
        finally
            if File.Exists marker then File.Delete marker
    }

    [<Theory>]
    [<InlineData("INT", 2, 4)>]
    [<InlineData("HUP", 1, 5)>]
    let ``Unix close signals reach a trapped stage without leaving a child`` signalName signalNumber exitCode = task {
        if not (OperatingSystem.IsWindows()) then
            use cancellation = new CancellationTokenSource()
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let request = if signalNumber = 2 then StopRequest.Interrupt else StopRequest.Hangup
            let script = "trap 'echo trapped; exit " + string exitCode + "' " + signalName + "; echo ready; while :; do sleep 1; done"
            let specification =
                // Detached verification runs under nohup, whose ignored SIGHUP disposition survives exec.
                // Reset both tested signals before the shell installs its traps.
                Process.commandArgs "env" [ "--default-signal=HUP,INT"; "sh"; "-c"; script ]
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> Process.successCodes (set [ exitCode ])
                |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            let mutable testFailure: exn option = None
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                let! closeExit = handle.Close request |> fun workflow -> workflow.StartAsTask(())
                test <@ closeExit = Exit.Success () @>
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
                match outcome with
                | Exit.Success result ->
                    test <@ result.ExitCode = exitCode @>
                    test <@ result.StdOut.Contains "trapped" @>
                | other -> failwithf "Expected trapped signal, got %A" other
            with error ->
                testFailure <- Some error

            if not running.IsCompleted then cancellation.Cancel()
            let! _ = running.WaitAsync(TimeSpan.FromSeconds 5.0)
            let childSurvives =
                try
                    use child = Diagnostics.Process.GetProcessById handle.ProcessId
                    not child.HasExited
                with :? ArgumentException -> false
            test <@ not childSurvives @>
            match testFailure with
            | Some error -> return raise error
            | None -> ()
    }

    [<Fact>]
    let ``CloseStdIn cancels an active feeder without faulting the run`` () = task {
        let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
        let bytes = Encoding.UTF8.GetBytes(String.replicate 4096 "x")
        let source = FlowStream.repeatFlow (Flow.succeed bytes)
        let running =
            shText "cat >/dev/null"
            |> stdinFrom (Input.flowStream source)
            |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())
        let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        let! closeExit = handle.Close StopRequest.CloseStdIn |> fun workflow -> workflow.StartAsTask(())
        test <@ closeExit = Exit.Success () @>
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Success result -> test <@ result.ExitCode = 0 @>
        | other -> failwithf "Expected clean stdin close, got %A" other
    }

    [<Fact>]
    let ``graceful ladder escalates from stdin EOF to TERM`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let marker = Path.Combine(Path.GetTempPath(), "axial-eof-term-" + Guid.NewGuid().ToString("N"))
            use cancellation = new CancellationTokenSource()
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let source = FlowStream.repeatFlow (Flow.fromAsync (async {
                do! Async.Sleep 30000
                return [||]
            }))
            let specification =
                shText ("trap 'echo term > " + marker + "; exit 0' TERM; echo ready; while :; do sleep 1; done")
                |> stdinFrom (Input.flowStream source)
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> stopGracefully [
                    StopRequest.CloseStdIn, TimeSpan.FromMilliseconds 100.0
                    StopRequest.Terminate, TimeSpan.FromSeconds 2.0
                ]
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                do! Task.Run(fun () -> cancellation.Cancel())
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
                match outcome with
                | Exit.Failure Cause.Interrupt -> test <@ File.Exists marker @>
                | other -> failwithf "Expected interruption, got %A" other
            finally
                if File.Exists marker then File.Delete marker
    }

    [<Fact>]
    let ``CloseStdIn is not delayed by a blocking cancellation callback`` () = task {
        let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
        let sourceEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let callbackEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let callbackRelease = new ManualResetEventSlim(false)
        let sourceRelease = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source =
            Flow.fromTask (fun token -> task {
                use _registration = token.Register(fun () ->
                    callbackEntered.TrySetResult() |> ignore
                    callbackRelease.Wait())
                sourceEntered.TrySetResult() |> ignore
                do! sourceRelease.Task
                return Encoding.UTF8.GetBytes "late"
            })
            |> FlowStream.fromFlow
        let running =
            cmd $"cat"
            |> stdinFrom (Input.flowStream source)
            |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())
        try
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! sourceEntered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            let stopwatch = Diagnostics.Stopwatch.StartNew()
            let closing = handle.Close StopRequest.CloseStdIn |> fun workflow -> workflow.StartAsTask(())
            do! callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            let! closeExit = closing.WaitAsync(TimeSpan.FromSeconds 3.0)
            stopwatch.Stop()
            test <@ closeExit = Exit.Success () @>
            test <@ stopwatch.Elapsed < TimeSpan.FromSeconds 2.0 @>
            callbackRelease.Set()
            sourceRelease.TrySetResult() |> ignore
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
            match outcome with
            | Exit.Success result -> test <@ result.ExitCode = 0 @>
            | other -> failwithf "Expected clean stdin close, got %A" other
        finally
            callbackRelease.Set()
            sourceRelease.TrySetResult() |> ignore
            callbackRelease.Dispose()
    }

    [<Fact>]
    let ``CloseStdIn contains a throwing cancellation callback`` () = task {
        let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
        let sourceEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source =
            Flow.fromTask (fun token -> task {
                use _registration = token.Register(fun () -> raise (InvalidOperationException "callback failed"))
                sourceEntered.TrySetResult() |> ignore
                do! Task.Delay(Timeout.InfiniteTimeSpan, token)
                return [||]
            })
            |> FlowStream.fromFlow
        let running =
            cmd $"cat"
            |> stdinFrom (Input.flowStream source)
            |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())
        let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        do! sourceEntered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        let! closeExit =
            handle.Close StopRequest.CloseStdIn
            |> fun workflow -> workflow.StartAsTask(())
            |> fun closing -> closing.WaitAsync(TimeSpan.FromSeconds 3.0)
        test <@ closeExit = Exit.Success () @>
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Success result -> test <@ result.ExitCode = 0 @>
        | other -> failwithf "Expected clean stdin close, got %A" other
    }

    [<Fact>]
    let ``graceful stop returns when a disconnected user input ignores cancellation`` () = task {
        if not (OperatingSystem.IsWindows()) then
            use cancellation = new CancellationTokenSource()
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let sourceEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let sourceRelease = TaskCompletionSource<byte array>(TaskCreationOptions.RunContinuationsAsynchronously)
            let source =
                Flow.fromTask (fun _ ->
                    sourceEntered.TrySetResult() |> ignore
                    sourceRelease.Task)
                // Without disconnect the run would wait for this source, which ignores interruption.
                |> Flow.disconnect
                // disconnect needs a clock for its fiber; stdin sources run without an environment.
                |> Flow.localEnv (fun () -> ClockEnvironment(Clock.live))
                |> FlowStream.fromFlow
            let specification =
                shText "trap '' TERM; echo ready; exec sleep 30"
                |> stdinFrom (Input.flowStream source)
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromMilliseconds 200.0 ]
            let processDone = TaskCompletionSource<Exit<ProcessResult, ProcessError>>(TaskCreationOptions.RunContinuationsAsynchronously)
            let running =
                specification
                |> toFlow
                |> Flow.onExit (fun exit -> Flow.delay (fun () -> processDone.TrySetResult exit |> ignore; Flow.succeed ()))
                |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                do! sourceEntered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                cancellation.Cancel()
                let! outcome = processDone.Task.WaitAsync(TimeSpan.FromSeconds 5.0)
                test <@ outcome = Exit.Failure Cause.Interrupt @>
                // The root run still owns the disconnected source and waits for it.
                test <@ not running.IsCompleted @>
                sourceRelease.TrySetResult [||] |> ignore
                let! rootOutcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
                test <@ rootOutcome = Exit.Failure Cause.Interrupt @>
            finally
                sourceRelease.TrySetResult [||] |> ignore
    }

    [<Fact>]
    let ``CloseStdIn preserves a user input defect raised during cancellation`` () = task {
        let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
        let sourceEntered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let defect = InvalidOperationException "input cancellation defect"
        let source =
            Flow.fromTask (fun token -> task {
                sourceEntered.TrySetResult() |> ignore
                try
                    do! Task.Delay(Timeout.InfiniteTimeSpan, token)
                    return [||]
                with :? OperationCanceledException ->
                    return raise defect
            })
            |> FlowStream.fromFlow
        let running =
            cmd $"cat"
            |> stdinFrom (Input.flowStream source)
            |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            |> toFlow
            |> fun workflow -> workflow.StartAsTask(env ())
        let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        do! sourceEntered.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
        let! closeExit = handle.Close StopRequest.CloseStdIn |> fun workflow -> workflow.StartAsTask(())
        test <@ closeExit = Exit.Success () @>
        let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
        match outcome with
        | Exit.Failure(Cause.Die error) -> test <@ obj.ReferenceEquals(error, defect) @>
        | other -> failwithf "Expected input defect, got %A" other
    }

    [<Fact>]
    let ``graceful ladder kills a child that ignores TERM after grace`` () = task {
        if not (OperatingSystem.IsWindows()) then
            use cancellation = new CancellationTokenSource()
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let specification =
                shText "trap '' TERM; echo ready; while :; do sleep 1; done"
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromMilliseconds 100.0 ]
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! Task.Run(fun () -> cancellation.Cancel())
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
            test <@ outcome = Exit.Failure Cause.Interrupt @>
            test <@ processHasExited handle.ProcessId @>
    }

    [<Fact>]
    let ``stage timeout runs the graceful ladder before returning its typed error`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let marker = Path.Combine(Path.GetTempPath(), "axial-timeout-term-" + Guid.NewGuid().ToString("N"))
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let specification =
                shText ("trap 'echo term > " + marker + "; exit 0' TERM; echo ready; while :; do sleep 1; done")
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> Process.timeout (TimeSpan.FromSeconds 5.0)
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 2.0 ]
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env ())
            try
                // The trap is installed before `echo ready`, so observing `ready` proves the trap is in
                // place before the (generous) 5s timeout can fire.
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 10.0)
                match outcome with
                | Exit.Failure(Cause.Fail(ProcessError.StageTimedOut _)) -> test <@ File.Exists marker @>
                | other -> failwithf "Expected stage timeout, got %A" other
            finally
                if File.Exists marker then File.Delete marker
    }

    [<Fact>]
    let ``default interruption kills immediately without running TERM trap`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let marker = Path.Combine(Path.GetTempPath(), "axial-default-kill-" + Guid.NewGuid().ToString("N"))
            use cancellation = new CancellationTokenSource()
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let specification =
                shText ("trap 'echo term > " + marker + "; exit 0' TERM; echo ready; while :; do sleep 1; done")
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                do! Task.Run(fun () -> cancellation.Cancel())
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
                test <@ outcome = Exit.Failure Cause.Interrupt @>
                test <@ not (File.Exists marker) @>
            finally
                if File.Exists marker then File.Delete marker
    }

    [<Fact>]
    let ``pipeline reports every stage handle and retained handles close as no-ops`` () = task {
        let handles = ConcurrentDictionary<int, ProcessStarted>()
        let specification =
            (shText "printf hello") => (cmd $"cat")
            |> onStarted (fun handle -> Flow.fromAsync (async { handles[handle.Stage] <- handle }))
        let! outcome = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env ())
        match outcome with
        | Exit.Success result ->
            test <@ result.StdOut = "hello" @>
            test <@ handles.Count = 2 @>
            test <@ result.Stages |> List.forall (fun stage -> handles[stage.Stage].ProcessId = stage.ProcessId) @>
            for handle in handles.Values do
                let! closeExit = handle.Close StopRequest.Terminate |> fun workflow -> workflow.StartAsTask(())
                test <@ closeExit = Exit.Success () @>
        | other -> failwithf "Expected pipeline success, got %A" other
    }

    [<Fact>]
    let ``CloseStdIn on a full pipe completes and the ladder reaches kill`` () = task {
        if not (OperatingSystem.IsWindows()) then
            use cancellation = new CancellationTokenSource()
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let feeding = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let mutable chunksRequested = 0
            // Each next element is requested only after the previous write completes. A stable
            // count therefore proves that the feeder is blocked in the write to the unread pipe.
            let chunk = Array.zeroCreate<byte> (8 * 1024 * 1024)
            let source =
                Flow.fromTask (fun _ -> task {
                    Interlocked.Increment(&chunksRequested) |> ignore
                    feeding.TrySetResult() |> ignore
                    return chunk
                })
                |> FlowStream.repeatFlow
            let specification =
                shText "trap '' TERM; echo ready; exec sleep 30"
                |> stdinFrom (Input.flowStream source)
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
                |> stopGracefully [
                    StopRequest.CloseStdIn, TimeSpan.FromMilliseconds 300.0
                    StopRequest.Terminate, TimeSpan.FromMilliseconds 300.0
                ]
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! feeding.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! Task.Delay(TimeSpan.FromMilliseconds 500.0)
            let blockedAt = Volatile.Read(&chunksRequested)
            do! Task.Delay(TimeSpan.FromMilliseconds 500.0)
            let stillBlockedAt = Volatile.Read(&chunksRequested)
            test <@ blockedAt > 0 && stillBlockedAt = blockedAt @>
            do! Task.Run(fun () -> cancellation.Cancel())
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 10.0)
            test <@ outcome = Exit.Failure Cause.Interrupt @>
            test <@ processHasExited handle.ProcessId @>
    }

    [<Fact>]
    let ``stop ladder runs once when cancellation races a stage timeout`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let marker = Path.Combine(Path.GetTempPath(), "axial-ladder-once-" + Guid.NewGuid().ToString("N"))
            use cancellation = new CancellationTokenSource()
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let raceAfter = TimeSpan.FromSeconds 2.0
            let specification =
                shText ("trap 'echo term >> " + marker + "; exit 0' TERM; echo ready; while :; do sleep 1; done")
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> Process.timeout raceAfter
                |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 2.0 ]
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env (), cancellation.Token)
            try
                let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                // Both causes use the process start timestamp as their reference point.
                let remaining = max TimeSpan.Zero (raceAfter - (DateTimeOffset.UtcNow - handle.StartedAt))
                let cancelAtDeadline = task {
                    do! Task.Delay remaining
                    cancellation.Cancel()
                }
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
                do! cancelAtDeadline
                let! outcome = running.WaitAsync(TimeSpan.FromSeconds 10.0)
                match outcome with
                | Exit.Failure Cause.Interrupt -> ()
                | Exit.Failure(Cause.Fail(ProcessError.StageTimedOut _)) -> ()
                | other -> failwithf "Expected interruption or stage timeout, got %A" other
                let terms = if File.Exists marker then File.ReadAllLines marker |> Array.length else 0
                test <@ terms = 1 @>
            finally
                if File.Exists marker then File.Delete marker
    }

    [<Fact>]
    let ``pidfd signalling reaches a trapped stage on Linux`` () = task {
        if OperatingSystem.IsLinux() && NativeProcess.pidfdAvailable () then
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let specification =
                shText "trap 'exit 7' TERM; echo ready; while :; do sleep 1; done"
                |> stdoutTo (Output.callback (fun _ -> Flow.fromAsync (async { ready.TrySetResult() |> ignore })))
                |> Process.successCodes (set [ 7 ])
                |> onStarted (fun handle -> Flow.fromAsync (async { started.TrySetResult handle |> ignore }))
            let running = specification |> toFlow |> fun workflow -> workflow.StartAsTask(env ())
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            do! ready.Task.WaitAsync(TimeSpan.FromSeconds 3.0)
            test <@ NativeProcess.trySignalWithPidfd handle.ProcessId 15 = Ok () @>
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 5.0)
            match outcome with
            | Exit.Success result -> test <@ result.ExitCode = 7 @>
            | other -> failwithf "Expected pidfd-terminated exit, got %A" other
    }

    [<Fact>]
    let ``TestProcess exposes fake pid and records close requests`` () =
        let host = ProcessHostEnvironment.test ()
        host.Process.ProcessId <- 9000
        host.Process.RunHandler <- fun _ -> TestProcess.Result(0, "")
        let mutable seen = -1
        let workflow =
            cmd $"fake"
            |> onStarted (fun handle ->
                seen <- handle.ProcessId
                Flow.fromAsync (async {
                    match Flow.runSync () (handle.Close StopRequest.Hangup) with
                    | Exit.Success () -> ()
                    | other -> failwithf "Expected fake close success, got %A" other
                }))
            |> toFlow
        match Flow.runSync host.Environment workflow with
        | Exit.Success _ ->
            test <@ seen = 9000 @>
            test <@ Seq.toList host.Process.CloseRequests = [ 0, StopRequest.Hangup ] @>
        | other -> failwithf "Expected fake success, got %A" other

    [<Theory>]
    [<InlineData("startup")>]
    [<InlineData("stage")>]
    [<InlineData("timeout")>]
    let ``process stream forwards startup stage and timeout failures`` kind = task {
        use rescue = new CancellationTokenSource()
        let specification =
            match kind with
            | "startup" -> cmd $"axial-missing-stream-command"
            | "stage" -> shText "exit 7"
            | _ -> shText "exec sleep 30" |> timeout (TimeSpan.FromMilliseconds 100.0)
        let running = specification |> streamOutputs |> ProcessStream.runCollect |> fun workflow -> workflow.StartAsTask(env (), rescue.Token)
        try
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.0)
            match kind, outcome with
            | "startup", Exit.Failure(Cause.Fail(ProcessError.StartFailed _))
            | "stage", Exit.Failure(Cause.Fail(ProcessError.StageFailed _))
            | "timeout", Exit.Failure(Cause.Fail(ProcessError.StageTimedOut _)) -> ()
            | _, other -> failwithf "Expected %s stream failure, got %A" kind other
        finally
            rescue.Cancel()
    }
