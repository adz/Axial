namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL
open Axial.Flow.PlatformService
open Axial.Flow.Console
open Axial.Flow.FileSystem
open Swensen.Unquote
open Xunit

type ProcessTestEnv =
    { Process: IProcess }
    interface IHas<IProcess> with
        member this.Service = this.Process

module ProcessServiceTests =
    let private env = { Process = Process.live Clock.live FileSystem.live Console.live }

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
        match Flow.runSync env workflow with
        | Exit.Success result -> result
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``live process timestamps transcripts through the supplied clock`` () =
        let fixedTime = DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero)
        let fixedEnv = { Process = Process.live (Clock.fromValue fixedTime) FileSystem.live Console.live }
        let workflow = cmd $"true" |> capture

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
            |> capture

        match Flow.runSync env workflow with
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
            let first = Process.command "sh" [ "-c"; $"echo $$ > '{pidPath}'; sleep 30" ]
            let missing = Process.command $"axial-missing-{Guid.NewGuid():N}" []
            let outcome = first => missing |> capture |> Flow.runSync env

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
                |> Process.run

            match Flow.runSync env workflow with
            | Exit.Failure(Cause.Fail(ProcessError.TimedOut failure)) ->
                test <@ failure.Specification.Contains "sleep 30" @>
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
            let running = Process.run<ProcessTestEnv> specification |> fun workflow -> workflow.ToTask(env, cancellation.Token)
            test <@ waitUntil (TimeSpan.FromSeconds 2.0) (fun () -> File.Exists childPidPath) @>
            let childPid = File.ReadAllText(childPidPath).Trim() |> Int32.Parse
            let nativeChild = System.Diagnostics.Process.GetProcessById childPid
            childProcess <- Some nativeChild

            cancellation.Cancel()
            match running.GetAwaiter().GetResult() with
            | Exit.Failure(Cause.Fail(ProcessError.Canceled _)) -> ()
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
                Process.command "sh" [ "-c"; "printf '%s|%s' \"$PWD\" \"$AXIAL_DEVICE\"" ]
                |> Process.workingDirectory directory
                |> Process.environment "AXIAL_DEVICE" "override"
                |> Process.run
                |> run
            let environmentResult =
                Process.command "/usr/bin/env" []
                |> Process.environment "AXIAL_DEVICE" "override"
                |> Process.removeEnvironment "PATH"
                |> Process.run
                |> run

            test <@ workingDirectoryResult.StdOut = $"{directory}|override" @>
            test <@ environmentResult.StdOut.Split('\n') |> Array.contains "AXIAL_DEVICE=override" @>
            test <@ environmentResult.StdOut.Split('\n') |> Array.exists (fun line -> line.StartsWith "PATH=") |> not @>
        finally
            Directory.Delete directory

    [<Fact>]
    let ``startup failure identifies the command without escaping the typed channel`` () =
        let executable = $"axial-missing-{Guid.NewGuid():N}"
        let outcome = Process.command executable [] |> Process.run |> Flow.runSync env
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
            test <@ failure.Command = executable @>
            test <@ String.IsNullOrWhiteSpace failure.Message = false @>
        | other -> failwithf "Expected startup failure, got %A" other

    [<Fact>]
    let ``streaming emits stdout and stderr before the complete result`` () =
        let stream =
            Process.command "sh" [ "-c"; "printf out; printf err >&2" ]
            |> Process.stream

        match stream |> FlowStream.runCollect |> Flow.runSync env with
        | Exit.Success events ->
            test <@ events |> List.exists (function ProcessEvent.Output output -> output.Channel = OutputChannel.StdOut && output.Text = "out" | _ -> false) @>
            test <@ events |> List.exists (function ProcessEvent.Output output -> output.Channel = OutputChannel.StdErr && output.Stage = 0 && output.Text = "err" | _ -> false) @>
            test <@ events |> List.exists (function ProcessEvent.Completed result -> result.StdOut = "out" && result.StdErr = "err" | _ -> false) @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``run reports every non-zero stage through the typed error channel`` () =
        let workflow =
            Process.command "sh" [ "-c"; "exit 7" ]
            => Process.command "cat" []
            |> capture

        match Flow.runSync env workflow with
        | Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
            test <@ failure.Stage.Stage = 0 @>
            test <@ failure.Result.ExitCodes = [ 7; 0 ] @>
        | result -> failwithf "Expected a typed non-zero exit, got %A" result

    [<Fact>]
    let ``tail capture is bounded and reports truncation`` () =
        let result =
            cmd $"printf %%s 0123456789"
            |> Process.stdout (OutputTarget.CaptureTail 4)
            |> Process.run
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
                |> Process.stdout (OutputTarget.Tee [ OutputTarget.File path; OutputTarget.CaptureTail 6 ])
                |> Process.run
                |> run

            test <@ File.ReadAllText path = "artifact-output" @>
            test <@ result.StdOut = "output" @>
            test <@ result.StdOutCapture.Truncated @>
        finally File.Delete path

    [<Fact>]
    let ``binary output is retained exactly and decoded with the configured encoding`` () =
        let result =
            Process.command "sh" [ "-c"; "printf '\\377\\000A'" ]
            |> Process.encoding Encoding.Latin1
            |> Process.run
            |> run

        test <@ result.StdOutCapture.Bytes = [| 255uy; 0uy; 65uy |] @>
        test <@ result.StdOut.Length = 3 @>

    [<Fact>]
    let ``accepted exit codes are immutable per command`` () =
        let result =
            Process.command "sh" [ "-c"; "exit 7" ]
            |> Process.successCodes [ 0; 7 ]
            |> Process.run
            |> run

        test <@ result.Stages.Head.ExitCode = 7 @>
        test <@ result.Stages.Head.Succeeded @>

    [<Fact>]
    let ``rendering and transcripts redact secret arguments`` () =
        let token = secret "super-secret-token"
        let command = cmd $"printf %%s {token}"

        test <@ Process.render command = "printf \"%s\" ***" @>
        let plan = command |> Process.plan
        test <@ plan.Commands = [ "printf \"%s\" ***" ] @>
        let result = command |> capture |> run
        test <@ result.Stages.Head.Command = "printf \"%s\" ***" @>
        test <@ result.Stages.Head.Command.Contains("super-secret-token") = false @>

    [<Fact>]
    let ``native process stream emits output and a final transcript`` () =
        let stream =
            cmd $"printf %%s streamed"
            |> Process.framing OutputFraming.Lines
            |> Process.stream

        let events = stream |> FlowStream.runCollect |> Flow.runSync env
        match events with
        | Exit.Success [ ProcessEvent.Output output; ProcessEvent.Completed result ] ->
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
                |> Process.stream
                |> FlowStream.take 1
                |> FlowStream.runCollect
                |> Flow.runSync env

            match events with
            | Exit.Success [ ProcessEvent.Output output ] -> test <@ output.Text = "ready\n" @>
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
            |> capture
            |> run

        test <@ result.StdOut = "ABC" @>
        test <@ result.ExitCodes = [ 0; 0; 0 ] @>

    [<Fact>]
    let ``text bytes and files are first class input endpoints`` () =
        let path = Path.GetTempFileName()
        try
            File.WriteAllText(path, "from-file")
            let text = Input.text "from-text" => cmd $"cat" |> capture |> run
            let bytes = Input.bytes [| 0uy; 1uy; 255uy |] => cmd $"cat" |> capture |> run
            let file = Input.file path => cmd $"cat" |> capture |> run
            test <@ text.StdOut = "from-text" @>
            test <@ bytes.StdOutCapture.Bytes = [| 0uy; 1uy; 255uy |] @>
            test <@ file.StdOut = "from-file" @>
        finally File.Delete path

    [<Fact>]
    let ``large input and output are pumped concurrently without deadlock`` () =
        let payload = Array.init (1024 * 1024) (fun index -> byte (index % 251))
        let result = Input.bytes payload => cmd $"cat" |> capture |> run
        test <@ result.StdOutCapture.Bytes = payload @>

    [<Fact>]
    let ``output file endpoint closes a topology into a flow`` () =
        let path = Path.GetTempFileName()
        try
            let result = cmd $"printf %%s written" => Output.file path |> run
            test <@ File.ReadAllText path = "written" @>
            test <@ result.StdOut = "" @>
        finally File.Delete path

    [<Fact>]
    let ``stream adapters move exact bytes incrementally`` () =
        let payload = [| 0uy; 7uy; 255uy; 10uy |]
        use source = new MemoryStream(payload)
        use target = new MemoryStream()
        let result = Input.stream source => cmd $"cat" => Output.stream target |> run
        test <@ target.ToArray() = payload @>
        test <@ result.StdOut = "" @>

    [<Fact>]
    let ``true inherited handles execute without redirected capture`` () =
        let result =
            cmd $"true"
            |> Process.stdout Output.inheritHandles
            |> Process.stderr Output.inheritHandles
            |> Process.run
            |> run
        test <@ result.ExitCode = 0 @>
        test <@ result.StdOut = "" @>

    [<Fact>]
    let ``merge stderr routes final stderr through stdout targets`` () =
        let result =
            shText "printf out; printf err >&2"
            |> mergeStderr
            |> capture
            |> run

        test <@ result.StdOut.Contains "out" @>
        test <@ result.StdOut.Contains "err" @>
        test <@ result.StdErr = "" @>

    [<Fact>]
    let ``pipe both connects stdout and stderr to the next command`` () =
        let result =
            shText "printf out; printf err >&2"
            |> pipeBothTo (cmd $"cat")
            |> capture
            |> run

        test <@ result.StdOut.Contains "out" @>
        test <@ result.StdOut.Contains "err" @>

    [<Fact>]
    let ``shell interpolation is passed out of band and cannot inject syntax`` () =
        let value = "safe; printf injected"
        let command = bash $"printf %%s {value}"
        let result = command |> capture |> run
        test <@ result.StdOut = value @>
        test <@ Process.render command |> fun rendered -> rendered.Contains value @>

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
            merge [ shText "printf 'alpha\\n'"; shText "printf 'beta\\n'" ]
            => cmd $"sort"
            |> capture
            |> run

        test <@ result.StdOut = "alpha\nbeta\n" @>
        test <@ result.ExitCodes = [ 0; 0; 0 ] @>

    [<Fact>]
    let ``true inheritance cannot be combined with tee`` () =
        raises<ArgumentException> <@
            cmd $"printf inherited"
            |> Process.stdout (OutputTarget.Tee [ Output.inheritHandles; Output.capture ])
            |> ignore @>
