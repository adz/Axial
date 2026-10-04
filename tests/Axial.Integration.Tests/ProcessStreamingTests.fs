namespace Axial.Tests

open System
open System.Text
open Axial
open Axial.Process
open Axial.Process.DSL
open Axial.PlatformService
open Xunit

module ProcessStreamingTests =
    let private succeeded = function
        | Exit.Success value -> value
        | failure -> failwithf "Expected success, got %A" failure

    let private completed = function
        | ProcessCompletion.Completed result -> result
        | ProcessCompletion.StoppedEarly -> failwith "Expected full completion"

    [<Fact>]
    let ``batch and streaming resolve distinct capture defaults`` () =
        let command = shText "head -c 70000 /dev/zero; head -c 70000 /dev/zero >&2"
        let batch = command |> Process.run |> succeeded
        Assert.Equal(70000, batch.StdOutCapture.Bytes.Length)
        Assert.Equal(65536, batch.StdErrCapture.Bytes.Length)
        Assert.True(batch.StdErrTruncated)
        let finish = command |> streamStdout |> ProcessStream.runDrain |> Process.run |> succeeded |> completed
        Assert.Equal(65536, finish.StdOutCapture.Bytes.Length)
        Assert.Equal(65536, finish.StdErrCapture.Bytes.Length)
        Assert.True(finish.StdOutTruncated)

    [<Fact>]
    let ``capture helpers only affect their named channel and explicit settings win`` () =
        let command = shText "printf output; printf diagnostic >&2" |> captureNothing |> captureStderrAll
        let result = command |> streamStdoutLines |> ProcessStream.runCollect |> Process.run |> succeeded
        let values, finish = result
        Assert.Equal<string list>(["output"], values)
        let captured = completed finish
        Assert.Equal("", captured.StdOut)
        Assert.True(captured.StdOutTruncated)
        Assert.Equal("diagnostic", captured.StdErr)
        let full = command |> captureAll |> streamStdoutLines |> ProcessStream.runDrain |> Process.run |> succeeded |> completed
        Assert.Equal("output", full.StdOut)

    [<Fact>]
    let ``line framing preserves blank lines bare CR and final text`` () =
        let values,finish = shText "printf 'one\r\n\nthree\rfour\nlast'" |> streamStdoutLines |> ProcessStream.runCollect |> Process.run |> succeeded
        Assert.Equal<string list>(["one"; ""; "three\rfour"; "last"], values)
        completed finish |> ignore

    [<Fact>]
    let ``line limit counts UTF16 units and rejects unterminated lines`` () =
        let outcome = shText "printf abcd" |> maxLineLength 3 |> streamStdoutLines |> ProcessStream.runCollect |> Process.run
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.LineTooLong error)) -> Assert.Equal(3,error.Limit)
        | other -> failwithf "Expected line limit failure, got %A" other
        let values,_ = shText "printf 'abc\r\n'" |> maxLineLength 3 |> streamStdoutLines |> ProcessStream.runCollect |> Process.run |> succeeded
        Assert.Equal<string list>(["abc"],values)

    [<Fact>]
    let ``merged output keeps original channel and stderr observation excludes it`` () =
        let values,finish = shText "printf out; printf err >&2" |> mergeStderr |> streamStdout |> ProcessStream.runCollect |> Process.run |> succeeded
        Assert.True(values |> List.forall(fun output -> output.ObservedChannel=OutputChannel.StdOut))
        Assert.True(values |> List.exists(fun output -> output.OriginalChannel=OutputChannel.StdErr))
        Assert.Equal("", (completed finish).StdErr)
        let errors,_ = shText "printf err >&2" |> mergeStderr |> streamStderrLines |> ProcessStream.runCollect |> Process.run |> succeeded
        Assert.Empty errors

    [<Fact>]
    let ``stream transforms preserve completion and are reusable`` () =
        let source = shText "printf '1\n2\n3\n'" |> streamStdoutLines |> ProcessStream.map Int32.Parse |> ProcessStream.filter(fun value -> value>1)
        for _ in 1..2 do
            let sum,finish = source |> ProcessStream.runFold (+) 0 |> Process.run |> succeeded
            Assert.Equal(5,sum)
            Assert.Equal(0,(completed finish).ExitCode)
        let values,finish = source |> ProcessStream.takeWhile(fun value -> value<3) |> ProcessStream.runCollect |> Process.run |> succeeded
        Assert.Equal<int list>([2],values)
        Assert.Equal(ProcessCompletion.StoppedEarly,finish)

    [<Fact>]
    let ``take zero never calls the process service`` () =
        let host = ProcessHostEnvironment.test()
        let values,finish = cmd $"missing" |> streamStdoutLines |> ProcessStream.take 0 |> ProcessStream.runCollect |> Process.runWith host.Environment |> succeeded
        Assert.Empty values
        Assert.Equal(ProcessCompletion.StoppedEarly,finish)
        Assert.Empty host.Process.RunSpecs

    [<Fact>]
    let ``consumer typed errors stay distinct and generic outcome runner accepts them`` () =
        let outcome = shText "echo ready; sleep 30" |> streamStdoutLines |> ProcessStream.runForEachFlow(fun _ -> Flow.fail "consumer failed") |> Process.run
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessStreamError.ConsumerFailed error)) -> Assert.Equal("consumer failed",error)
        | other -> failwithf "Expected consumer error, got %A" other

    [<Fact>]
    let ``inline settings and env removal override defaults through composition`` () =
        let sourceDefaults = StageDefaults.empty |> StageDefaults.environment "A" "source" |> StageDefaults.timeout(TimeSpan.FromSeconds 4.)
        let destinationDefaults = StageDefaults.empty |> StageDefaults.environment "A" "destination" |> StageDefaults.environment "B" "value"
        let first = cmd $"true" |> env "A" "explicit" |> Process.ofCommand |> stageDefaults sourceDefaults
        let final = cmd $"true" |> Process.removeEnvironment "B" |> Process.withoutTimeout |> Process.ofCommand |> stageDefaults destinationDefaults
        let plan = first => final |> Process.plan
        Assert.Equal(Some "explicit", plan.Stages[0].Environment["A"])
        Assert.Equal(Some "destination", plan.Stages[1].Environment["A"])
        Assert.Equal(None, plan.Stages[1].Environment["B"])
        Assert.Equal(None, plan.Stages[1].Timeout)
        Assert.Equal(Some(TimeSpan.FromSeconds 4.),plan.Stages[0].Timeout)

    [<Fact>]
    let ``routing conflicts and disconnected fan in fail before launch`` () =
        Assert.Throws<ArgumentException>(fun () -> (cmd $"true" |> stdoutToDiscard) => cmd $"cat" |> ignore) |> ignore
        Assert.Throws<ArgumentException>(fun () -> cmd $"true" => (cmd $"cat" |> stdinFromText "conflict") |> ignore) |> ignore
        Assert.Throws<ArgumentException>(fun () -> (cmd $"true" |> stderrToDiscard) |> mergeStderr |> ignore) |> ignore
        let host = ProcessHostEnvironment.test()
        let outcome = mergeStdout [cmd $"true"; cmd $"true"] |> Process.runWith host.Environment
        match outcome with
        | Exit.Failure(Cause.Die _) -> ()
        | other -> failwithf "Expected invalid disconnected topology, got %A" other
        Assert.Empty host.Process.RunSpecs

    [<Fact>]
    let ``inheritance marks capture unavailable and explicit observation fails`` () =
        let command = cmd $"true" |> stdoutToInheritHandles
        let result = command |> Process.run |> succeeded
        Assert.False(result.StdOutCaptureAvailable)
        let outcome = command |> streamStdout |> ProcessStream.runDrain |> Process.run
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.ObservationUnavailable _)) -> ()
        | other -> failwithf "Expected unavailable observation, got %A" other

    [<Fact>]
    let ``outcome and integer runners execute once with different defaults`` () =
        let host = ProcessHostEnvironment.test()
        host.Process.RunHandler <- fun _ -> TestProcess.Result(0, "")
        cmd $"true" |> Process.runWith host.Environment |> succeeded |> ignore
        Assert.Single(host.Process.RunSpecs) |> ignore
        let batchPlan = host.Process.RunSpecs[0] |> Process.plan
        Assert.Equal(OutputRetention.All,batchPlan.Stages[0].StdOutCapture)
        let code = cmd $"true" |> Process.runExitCodeWith host.Environment
        Assert.Equal(0,code)
        Assert.Equal(2,host.Process.RunSpecs.Count)
        let hostPlan = host.Process.RunSpecs[1] |> Process.plan
        Assert.Equal("console",hostPlan.Stages[0].StdOut)
        Assert.Equal(OutputRetention.Nothing,hostPlan.Stages[0].StdOutCapture)
        Assert.Equal(OutputRetention.Tail 65536,hostPlan.Stages[0].StdErrCapture)
