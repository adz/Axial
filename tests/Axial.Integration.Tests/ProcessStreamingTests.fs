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

    [<Fact>]
    let ``overall deadline interrupts a slow final consumer and awaits cleanup`` () = task {
        use rescue = new System.Threading.CancellationTokenSource()
        let entered = System.Threading.Tasks.TaskCompletionSource<unit>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously)
        let stopped = System.Threading.Tasks.TaskCompletionSource<unit>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously)
        let action _ = Flow.fromAsync(async {
            entered.TrySetResult() |> ignore
            try do! Async.Sleep 30000
            finally stopped.TrySetResult() |> ignore
        })
        let running =
            shText "echo ready"
            |> overallTimeout(TimeSpan.FromMilliseconds 250.)
            |> streamStdoutLines
            |> ProcessStream.runForEachFlow action
            |> fun workflow -> workflow.StartAsTask(ProcessHostEnvironment.live,rescue.Token)
        try
            do! entered.Task.WaitAsync(TimeSpan.FromSeconds 3.)
            let! outcome = running.WaitAsync(TimeSpan.FromSeconds 3.)
            match outcome with
            | Exit.Failure(Cause.Fail(ProcessStreamError.ProcessFailed(ProcessError.OverallTimedOut _))) -> ()
            | other -> failwithf "Expected overall timeout, got %A" other
            Assert.True(stopped.Task.IsCompleted)
        finally rescue.Cancel()
    }

    [<Fact>]
    let ``per-stage timeout ends at native exit while callback remains active`` () =
        let values,finish =
            shText "echo ready"
            |> timeout(TimeSpan.FromMilliseconds 150.)
            |> streamStdoutLines
            |> ProcessStream.runFoldFlow(fun values item -> Flow.fromAsync(async {
                do! Async.Sleep 250
                return item::values
            })) []
            |> Process.run
            |> succeeded
        Assert.Equal<string list>(["ready"],values)
        Assert.Equal(0,(completed finish).ExitCode)

    [<Fact>]
    let ``byte tails decode split Unicode and aggregate stderr remains bounded`` () =
        let unicode = Process.commandArgs "printf" ["€"] |> captureTail 2 |> Process.run |> succeeded
        Assert.Equal<byte array>([|0x82uy;0xacuy|],unicode.StdOutCapture.Bytes)
        Assert.True(unicode.StdOutTruncated)
        Assert.Contains("�",unicode.StdOut)
        let producer = shText "head -c 70000 /dev/zero >&2; printf ready"
        let second = shText "cat >/dev/null; head -c 70000 /dev/zero >&2; printf ready"
        let result = producer => second => cmd $"cat" |> Process.run |> succeeded
        Assert.Equal(65536,result.StdErrCapture.Bytes.Length)
        Assert.True(result.StdErrTruncated)
        Assert.False(result.Stages[0].StdOutCapture.Available)
        Assert.Empty(result.Stages[0].StdOutCapture.Bytes)

    [<Fact>]
    let ``fan in line limits apply to UTF16 characters instead of encoded bytes`` () =
        let outcome = mergeStdout [shText "printf abcd"; cmd $"true"] |> maxLineLength 3 => cmd $"cat" |> Process.run
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.LineTooLong error)) -> Assert.Equal(0,error.Stage)
        | other -> failwithf "Expected producer line limit, got %A" other

    [<Fact>]
    let ``map final command validates identity and fresh writer decoders work across runs`` () =
        let specification = cmd $"printf hello" => cmd $"cat"
        Assert.Throws<ArgumentException>(fun () -> specification |> Process.mapFinalCommand(Process.arg "replacement") |> ignore) |> ignore
        let configured = specification |> Process.mapFinalCommand(Process.captureTail 2)
        let result = configured |> Process.run |> succeeded
        Assert.Equal("lo",result.StdOut)
        use writer = new System.IO.StringWriter()
        let command = Process.commandArgs "printf" ["€"] |> stdoutTo(Output.textWriter Encoding.UTF8 writer)
        for _ in 1..2 do command |> Process.run |> succeeded |> ignore
        Assert.Equal("€€",writer.ToString())
        writer.Write("!")
        Assert.Equal("€€!",writer.ToString())

    [<Fact>]
    let ``producer and consumer failures during the same published callback both survive`` () =
        let outcome =
            shText "echo ready; sleep 0.05; exit 7"
            |> streamStdoutLines
            |> ProcessStream.runForEachFlow(fun _ -> flow {
                do! Flow.fromAsync(async {do! Async.Sleep 200})
                return! Flow.fail "consumer"
            })
            |> Process.run
        match outcome with
        | Exit.Failure(Cause.Both(Cause.Fail(ProcessStreamError.ConsumerFailed "consumer"), Cause.Fail(ProcessStreamError.ProcessFailed(ProcessError.StageFailed _)))) -> ()
        | other -> failwithf "Expected both observed failures, got %A" other

    [<Fact>]
    let ``shell patterns route per-stage stderr append files tee and generated input`` () =
        let path = System.IO.Path.GetTempFileName()
        let diagnosticPath = System.IO.Path.GetTempFileName()
        try
            let first = shText "printf first; printf stage-error >&2" |> stderrToFile diagnosticPath
            let final = cmd $"cat" |> stdoutToFile path
            first => final |> Process.run |> succeeded |> ignore
            Assert.Equal("first",System.IO.File.ReadAllText path)
            Assert.Equal("stage-error",System.IO.File.ReadAllText diagnosticPath)
            cmd $"printf second" |> stdoutToAppendFile path |> Process.run |> succeeded |> ignore
            Assert.Equal("firstsecond",System.IO.File.ReadAllText path)
            shText "printf out; printf err >&2" |> stdoutToFile path |> mergeStderr |> Process.run |> succeeded |> ignore
            let merged = System.IO.File.ReadAllText path
            Assert.Contains("out",merged)
            Assert.Contains("err",merged)
            cmd $"printf tee" |> stdoutTo(Output.tee [Output.file path; Output.discard]) |> Process.run |> succeeded |> ignore
            Assert.Equal("tee",System.IO.File.ReadAllText path)
            let generated =
                cmd $"cat"
                |> stdinFrom(Input.produce(fun emit -> async {
                    do! emit (Encoding.UTF8.GetBytes "one")
                    do! emit (Encoding.UTF8.GetBytes "two")
                }))
                |> Process.run
                |> succeeded
            Assert.Equal("onetwo",generated.StdOut)
            let sequential = flow {
                do! shText "exit 7" |> toFlow |> Flow.map ignore |> Flow.orElseWith(function ProcessError.StageFailed _ -> Flow.succeed() | error -> Flow.fail error)
                return! cmd $"printf continued" |> toFlow
            }
            Assert.Equal("continued",(sequential |> Process.run |> succeeded).StdOut)
        finally
            System.IO.File.Delete path
            System.IO.File.Delete diagnosticPath
