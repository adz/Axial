namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.IO
open System.Text
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL
open Axial.Flow.PlatformService
open Swensen.Unquote
open Xunit

type ProcessTestEnv =
    { Process: IProcess }
    interface IHas<IProcess> with
        member this.Service = this.Process

module ProcessServiceTests =
    let private env = { Process = Process.live Clock.live }

    [<Fact>]
    let ``live process timestamps transcripts through the supplied clock`` () =
        let fixedTime = DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero)
        let fixedEnv = { Process = Process.live (Clock.fromValue fixedTime) }
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
    let ``streaming observes stdout and stderr while preserving the complete result`` () =
        let observed = ConcurrentQueue<ProcessOutput>()
        let observe output = async { observed.Enqueue output }

        let workflow =
            Process.command "sh" [ "-c"; "printf out; printf err >&2" ]
            |> Process.pipeline
            |> Process.observe observe

        match Flow.runSync env workflow with
        | Exit.Success result ->
            test <@ result.StdOut = "out" @>
            test <@ result.StdErr = "err" @>
            test <@ observed |> Seq.exists (fun output -> output.Channel = OutputChannel.StdOut && output.Text = "out") @>
            test <@ observed |> Seq.exists (fun output -> output.Channel = OutputChannel.StdErr && output.Stage = 0 && output.Text = "err") @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``run reports every non-zero stage through the typed error channel`` () =
        let workflow =
            Process.command "sh" [ "-c"; "exit 7" ]
            => Process.command "cat" []
            |> capture

        match Flow.runSync env workflow with
        | Exit.Failure(Cause.Fail(ProcessError.StageFailed(stage, result))) ->
            test <@ stage.Stage = 0 @>
            test <@ result.ExitCodes = [ 7; 0 ] @>
        | result -> failwithf "Expected a typed non-zero exit, got %A" result

    let private run workflow =
        match Flow.runSync env workflow with
        | Exit.Success result -> result
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``tail capture is bounded and reports truncation`` () =
        let result =
            cmd $"printf %%s 0123456789"
            |> Process.pipeline
            |> Process.stdout (OutputTarget.CaptureTail 4)
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
                |> Process.pipeline
                |> Process.stdout (OutputTarget.Tee [ OutputTarget.File path; OutputTarget.CaptureTail 6 ])
                |> Process.toFlow
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
            |> Process.pipeline
            |> Process.toFlow
            |> run

        test <@ result.StdOutCapture.Bytes = [| 255uy; 0uy; 65uy |] @>
        test <@ result.StdOut.Length = 3 @>

    [<Fact>]
    let ``accepted exit codes are immutable per command`` () =
        let result =
            Process.command "sh" [ "-c"; "exit 7" ]
            |> Process.successCodes [ 0; 7 ]
            |> Process.pipeline
            |> Process.toFlow
            |> run

        test <@ result.Stages.Head.ExitCode = 7 @>
        test <@ result.Stages.Head.Succeeded @>

    [<Fact>]
    let ``rendering and transcripts redact secret arguments`` () =
        let token = secret "super-secret-token"
        let command = cmd $"printf %%s {token}"

        test <@ Process.render command = "printf \"%s\" ***" @>
        let plan = command |> Process.pipeline |> Process.plan
        test <@ plan.Commands = [ "printf \"%s\" ***" ] @>
        let result = command |> capture |> run
        test <@ result.Stages.Head.Command = "printf \"%s\" ***" @>
        test <@ result.Stages.Head.Command.Contains("super-secret-token") = false @>

    [<Fact>]
    let ``native process stream emits output and a final transcript`` () =
        let stream =
            cmd $"printf %%s streamed"
            |> Process.pipeline
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
            |> Process.pipeline
            |> Process.stdout Output.inheritHandles
            |> Process.stderr Output.inheritHandles
            |> toFlow
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
            |> Process.pipeline
            |> Process.stdout (OutputTarget.Tee [ Output.inheritHandles; Output.capture ])
            |> ignore @>
