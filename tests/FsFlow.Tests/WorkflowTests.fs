namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Sources
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowTests =
        [<Fact>]
        let ``Flow is sync result only`` () =
            let workflow : Flow<int, string, int> =
                Flow.env
                |> Flow.bind (fun value -> Flow.succeed(value * 2))

            test <@ Flow.run 21 workflow = Ok 42 @>

        [<Fact>]
        let ``Flow delay reruns from scratch`` () =
            let runs = ref 0

            let workflow : Flow<unit, string, int> =
                Flow.delay(fun () ->
                    runs.Value <- runs.Value + 1
                    Flow.succeed runs.Value)

            test <@ Flow.run () workflow = Ok 1 @>
            test <@ Flow.run () workflow = Ok 2 @>

        [<Fact>]
        let ``AsyncFlow runs as Async result`` () =
            let workflow : AsyncFlow<int, string, int> =
                AsyncFlow.read id
                |> AsyncFlow.bind (fun value ->
                    AsyncFlow.fromAsync(async { return value * 2 }))

            let result =
                workflow
                |> AsyncFlow.run 21
                |> Async.RunSynchronously

            test <@ result = Ok 42 @>

        [<Fact>]
        let ``AsyncFlow can lift Flow`` () =
            let syncWorkflow : Flow<string, string, int> =
                Flow.read String.length

            let asyncWorkflow : AsyncFlow<string, string, int> =
                syncWorkflow
                |> AsyncFlow.fromFlow
                |> AsyncFlow.map ((+) 1)

            let result =
                asyncWorkflow
                |> AsyncFlow.toAsync "effect"
                |> Async.RunSynchronously

            test <@ result = Ok 7 @>

        [<Fact>]
        let ``shared combinators preserve sync and async environment semantics`` () =
            let syncBase : Flow<int, int, int> =
                Flow.read (fun env -> env + 1)
                |> Flow.map ((*) 2)
                |> Flow.bind (fun value -> Flow.succeed(value + 3))
                |> Flow.mapError String.length

            let syncWorkflow : Flow<string, int, int> =
                Flow.localEnv String.length syncBase

            let asyncBase : AsyncFlow<int, int, int> =
                AsyncFlow.read (fun env -> env + 1)
                |> AsyncFlow.map ((*) 2)
                |> AsyncFlow.bind (fun value -> AsyncFlow.succeed(value + 3))
                |> AsyncFlow.mapError String.length

            let asyncWorkflow : AsyncFlow<string, int, int> =
                AsyncFlow.localEnv String.length asyncBase

            let syncResult = Flow.run "flowkit" syncWorkflow

            let asyncResult =
                asyncWorkflow
                |> AsyncFlow.run "flowkit"
                |> Async.RunSynchronously

            test <@ syncResult = Ok 19 @>
            test <@ asyncResult = Ok 19 @>

        [<Fact>]
        let ``flow families expose normalized constructors operators and fallback helpers`` () =
            let syncOk = Flow.ok 41
            let syncAlias = Flow.succeed 41
            let syncError = Flow.error "missing"
            let syncErrorAlias = Flow.fail "missing"

            let syncMapped =
                Flow.(<!>) ((+) 1) syncOk
                |> Flow.run ()

            let syncApplied =
                Flow.(<*>) (Flow.ok ((+) 1)) syncOk
                |> Flow.run ()

            let syncMapped3 =
                Flow.map3 (fun left middle right -> left + middle + right) (Flow.ok 1) (Flow.ok 2) (Flow.ok 3)
                |> Flow.run ()

            let syncIgnored =
                Flow.ignore syncOk
                |> Flow.run ()

            let syncBound =
                Flow.(>>=) syncOk (fun value -> Flow.ok (value + 1))
                |> Flow.run ()

            let syncRecovered =
                Flow.orElseWith (fun (error: string) -> Flow.ok error.Length) syncError
                |> Flow.run ()

            let asyncOk = AsyncFlow.ok 41
            let asyncAlias = AsyncFlow.succeed 41
            let asyncError = AsyncFlow.error "missing"
            let asyncErrorAlias = AsyncFlow.fail "missing"

            let asyncMapped =
                AsyncFlow.(<!>) ((+) 1) asyncOk
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncApplied =
                AsyncFlow.(<*>) (AsyncFlow.ok ((+) 1)) asyncOk
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncMapped3 =
                AsyncFlow.map3 (fun left middle right -> left + middle + right) (AsyncFlow.ok 1) (AsyncFlow.ok 2) (AsyncFlow.ok 3)
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncIgnored =
                AsyncFlow.ignore asyncOk
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncBound =
                AsyncFlow.(>>=) asyncOk (fun value -> AsyncFlow.ok (value + 1))
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncRecovered =
                AsyncFlow.orElseWith (fun (error: string) -> AsyncFlow.ok error.Length) asyncError
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncOkResult = AsyncFlow.run () asyncOk |> Async.RunSynchronously
            let asyncAliasResult = AsyncFlow.run () asyncAlias |> Async.RunSynchronously
            let asyncErrorResult = AsyncFlow.run () asyncError |> Async.RunSynchronously
            let asyncErrorAliasResult = AsyncFlow.run () asyncErrorAlias |> Async.RunSynchronously

            let taskOk = TaskFlow.ok 41
            let taskAlias = TaskFlow.succeed 41
            let taskError = TaskFlow.error "missing"
            let taskErrorAlias = TaskFlow.fail "missing"

            let taskMapped =
                TaskFlow.(<!>) ((+) 1) taskOk
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskApplied =
                TaskFlow.(<*>) (TaskFlow.ok ((+) 1)) taskOk
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskMapped3 =
                TaskFlow.map3 (fun left middle right -> left + middle + right) (TaskFlow.ok 1) (TaskFlow.ok 2) (TaskFlow.ok 3)
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskIgnored =
                TaskFlow.ignore taskOk
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskBound =
                TaskFlow.(>>=) taskOk (fun value -> TaskFlow.ok (value + 1))
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskRecovered =
                TaskFlow.orElseWith (fun (error: string) -> TaskFlow.ok error.Length) taskError
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ Flow.run () syncOk = Flow.run () syncAlias @>
            test <@ Flow.run () syncError = Flow.run () syncErrorAlias @>
            test <@ syncMapped = Ok 42 @>
            test <@ syncApplied = Ok 42 @>
            test <@ syncMapped3 = Ok 6 @>
            test <@ syncIgnored = Ok () @>
            test <@ syncBound = Ok 42 @>
            test <@ syncRecovered = Ok 7 @>
            test <@ asyncOkResult = asyncAliasResult @>
            test <@ asyncErrorResult = asyncErrorAliasResult @>
            test <@ asyncMapped = Ok 42 @>
            test <@ asyncApplied = Ok 42 @>
            test <@ asyncMapped3 = Ok 6 @>
            test <@ asyncIgnored = Ok () @>
            test <@ asyncBound = Ok 42 @>
            test <@ asyncRecovered = Ok 7 @>
            test <@ (TaskFlow.run () CancellationToken.None taskOk |> fun task -> task.GetAwaiter().GetResult()) = (TaskFlow.run () CancellationToken.None taskAlias |> fun task -> task.GetAwaiter().GetResult()) @>
            test <@ (TaskFlow.run () CancellationToken.None taskError |> fun task -> task.GetAwaiter().GetResult()) = (TaskFlow.run () CancellationToken.None taskErrorAlias |> fun task -> task.GetAwaiter().GetResult()) @>
            test <@ taskMapped = Ok 42 @>
            test <@ taskApplied = Ok 42 @>
            test <@ taskMapped3 = Ok 6 @>
            test <@ taskIgnored = Ok () @>
            test <@ taskBound = Ok 42 @>
            test <@ taskRecovered = Ok 7 @>

        [<Fact>]
        let ``Flow composition helpers cover error tapping fallback and pairing`` () =
            let tappedErrors = ResizeArray<string>()

            let tapPreservesOriginalError =
                Flow.fail "primary"
                |> Flow.tapError (fun error ->
                    tappedErrors.Add error
                    Flow.succeed ())
                |> Flow.run ()

            let tapSkipsSuccess =
                Flow.succeed 42
                |> Flow.tapError (fun error ->
                    tappedErrors.Add $"unexpected:{error}"
                    Flow.succeed ())
                |> Flow.run ()

            let recovered =
                Flow.fail "missing"
                |> Flow.orElse (Flow.read (fun env -> env + 1))
                |> Flow.run 41

            let bypassesFallback =
                Flow.succeed 10
                |> Flow.orElse (Flow.succeed 99)
                |> Flow.run ()

            let zipped =
                Flow.zip (Flow.read (fun env -> env + 1)) (Flow.read (fun env -> env * 2))
                |> Flow.run 5

            let mapped =
                Flow.map2 (+) (Flow.read (fun env -> env + 1)) (Flow.read (fun env -> env * 2))
                |> Flow.run 5

            test <@ tapPreservesOriginalError = Error "primary" @>
            test <@ tapSkipsSuccess = Ok 42 @>
            test <@ List.ofSeq tappedErrors = [ "primary" ] @>
            test <@ recovered = Ok 42 @>
            test <@ bypassesFallback = Ok 10 @>
            test <@ zipped = Ok(6, 10) @>
            test <@ mapped = Ok 16 @>

        [<Fact>]
        let ``AsyncFlow composition helpers cover error tapping fallback and pairing`` () =
            let tappedErrors = ResizeArray<string>()

            let tapPreservesOriginalError =
                AsyncFlow.fail "primary"
                |> AsyncFlow.tapError (fun error ->
                    tappedErrors.Add error
                    AsyncFlow.succeed ())
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let tapSkipsSuccess =
                AsyncFlow.succeed 42
                |> AsyncFlow.tapError (fun error ->
                    tappedErrors.Add $"unexpected:{error}"
                    AsyncFlow.succeed ())
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let recovered =
                AsyncFlow.fail "missing"
                |> AsyncFlow.orElse (AsyncFlow.read (fun env -> env + 1))
                |> AsyncFlow.run 41
                |> Async.RunSynchronously

            let bypassesFallback =
                AsyncFlow.succeed 10
                |> AsyncFlow.orElse (AsyncFlow.succeed 99)
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let zipped =
                AsyncFlow.zip (AsyncFlow.read (fun env -> env + 1)) (AsyncFlow.read (fun env -> env * 2))
                |> AsyncFlow.run 5
                |> Async.RunSynchronously

            let mapped =
                AsyncFlow.map2 (+) (AsyncFlow.read (fun env -> env + 1)) (AsyncFlow.read (fun env -> env * 2))
                |> AsyncFlow.run 5
                |> Async.RunSynchronously

            test <@ tapPreservesOriginalError = Error "primary" @>
            test <@ tapSkipsSuccess = Ok 42 @>
            test <@ List.ofSeq tappedErrors = [ "primary" ] @>
            test <@ recovered = Ok 42 @>
            test <@ bypassesFallback = Ok 10 @>
            test <@ zipped = Ok(6, 10) @>
            test <@ mapped = Ok 16 @>

        [<Fact>]
        let ``ColdTask carries the runtime cancellation token into TaskFlow`` () =
            let seen = ref CancellationToken.None
            use cts = new CancellationTokenSource()

            let workflow : TaskFlow<unit, string, int> =
                TaskFlow.fromTask(
                    ColdTask(fun cancellationToken ->
                        seen.Value <- cancellationToken
                        Task.FromResult 42)
                )

            let result =
                workflow
                |> TaskFlow.run () cts.Token
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Ok 42 @>
            test <@ seen.Value = cts.Token @>

        [<Fact>]
        let ``ColdTask helpers adapt task and valuetask sources with the expected hot and cold semantics`` () =
            let startedTask = Task.FromResult 42
            let hotTask = ColdTask.fromTask startedTask

            let taskRun1 = hotTask |> ColdTask.run CancellationToken.None
            let taskRun2 = hotTask |> ColdTask.run CancellationToken.None

            test <@ obj.ReferenceEquals(startedTask, taskRun1) @>
            test <@ obj.ReferenceEquals(taskRun1, taskRun2) @>
            test <@ taskRun1.GetAwaiter().GetResult() = 42 @>

            let taskFactoryRuns = ref 0

            let coldTask =
                ColdTask.fromTaskFactory(fun () ->
                    taskFactoryRuns.Value <- taskFactoryRuns.Value + 1
                    Task.FromResult taskFactoryRuns.Value)

            let coldTaskRun1 = coldTask |> ColdTask.run CancellationToken.None |> fun task -> task.GetAwaiter().GetResult()
            let coldTaskRun2 = coldTask |> ColdTask.run CancellationToken.None |> fun task -> task.GetAwaiter().GetResult()

            test <@ coldTaskRun1 = 1 @>
            test <@ coldTaskRun2 = 2 @>

            let seen = ref CancellationToken.None
            let valueTaskFactoryRuns = ref 0

            let coldValueTask =
                ColdTask.fromValueTaskFactory(fun cancellationToken ->
                    seen.Value <- cancellationToken
                    valueTaskFactoryRuns.Value <- valueTaskFactoryRuns.Value + 1
                    ValueTask<int>(valueTaskFactoryRuns.Value))

            use cts = new CancellationTokenSource()

            let coldValueTaskRun1 =
                coldValueTask
                |> ColdTask.run cts.Token
                |> fun task -> task.GetAwaiter().GetResult()

            let coldValueTaskRun2 =
                coldValueTask
                |> ColdTask.run CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ coldValueTaskRun1 = 1 @>
            test <@ coldValueTaskRun2 = 2 @>
            test <@ seen.Value = CancellationToken.None @>

            let startedValueTask = ValueTask<int>(99)
            let hotValueTask = ColdTask.fromValueTask startedValueTask

            let hotValueTaskRun1 = hotValueTask |> ColdTask.run CancellationToken.None
            let hotValueTaskRun2 = hotValueTask |> ColdTask.run CancellationToken.None

            test <@ obj.ReferenceEquals(hotValueTaskRun1, hotValueTaskRun2) @>
            test <@ hotValueTaskRun1.GetAwaiter().GetResult() = 99 @>

            let oneShotValueTaskSource = SingleConsumptionValueTaskSource 123

            let normalizedHotValueTask =
                oneShotValueTaskSource.AsValueTask()
                |> ColdTask.fromValueTask

            let normalizedRun1 =
                normalizedHotValueTask
                |> ColdTask.run CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let normalizedRun2 =
                normalizedHotValueTask
                |> ColdTask.run CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ normalizedRun1 = 123 @>
            test <@ normalizedRun2 = 123 @>
            test <@ oneShotValueTaskSource.ConsumptionCount = 1 @>

        [<Fact>]
        let ``ColdTask of Result is the typed failure cold task shape`` () =
            let seen = ref CancellationToken.None
            use cts = new CancellationTokenSource()

            let workflow : TaskFlow<unit, string, int> =
                TaskFlow.fromTaskResult(
                    ColdTask(fun cancellationToken ->
                        seen.Value <- cancellationToken
                        Task.FromResult(Ok 42))
                )

            let result =
                workflow
                |> TaskFlow.run () cts.Token
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Ok 42 @>
            test <@ seen.Value = cts.Token @>

        [<Fact>]
        let ``TaskFlow.fromTask requires the nominal ColdTask wrapper`` () =
            let fsFlowAssemblyPath = typeof<FlowBuilder>.Assembly.Location
            let fsFlowNetAssemblyPath = typeof<TaskFlowBuilder>.Assembly.Location

            let rawFactoryProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    #r @"{fsFlowNetAssemblyPath}"
    open System.Threading
    open System.Threading.Tasks
    open FsFlow

    let probe : TaskFlow<unit, string, int> =
        TaskFlow.fromTask(fun (_: CancellationToken) -> Task.FromResult 42)
    """

            let wrappedFactoryProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    #r @"{fsFlowNetAssemblyPath}"
    open System.Threading.Tasks
    open FsFlow

    let probe : TaskFlow<unit, string, int> =
        TaskFlow.fromTask(ColdTask(fun _ -> Task.FromResult 42))
    """

            let rawExitCode, rawOutput = runFsiScript rawFactoryProbe
            let wrappedExitCode, wrappedOutput = runFsiScript wrappedFactoryProbe

            test <@ rawExitCode <> 0 @>
            test <@ rawOutput.Contains("error FS") @>
            test <@ wrappedExitCode = 0 @>
            test <@ wrappedOutput = "" @>

        [<Fact>]
        let ``Runnable example docs are generated from executable example projects`` () =
            let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
            let docsExamplesPath = Path.Combine(repoRoot, "docs", "examples", "README.md")
            let generatorPath = Path.Combine(repoRoot, "scripts", "generate-example-docs.sh")
            let generatedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md")

            try
                let exitCode, output =
                    runBashScript generatorPath [ "DOCS_EXAMPLES_OUTPUT", generatedPath ]

                if exitCode <> 0 then
                    failwithf "generate-example-docs.sh failed with exit code %d:%s%s" exitCode Environment.NewLine output

                test <@ File.ReadAllText generatedPath = File.ReadAllText docsExamplesPath @>
            finally
                if File.Exists generatedPath then
                    File.Delete generatedPath

        [<Fact>]
        let ``TaskFlow can lift AsyncFlow`` () =
            let asyncWorkflow : AsyncFlow<int, string, int> =
                AsyncFlow.read id
                |> AsyncFlow.map (fun value -> value + 2)

            let taskWorkflow : TaskFlow<int, string, int> =
                asyncWorkflow
                |> TaskFlow.fromAsyncFlow
                |> TaskFlow.map (fun value -> value * 10)

            let result =
                taskWorkflow
                |> TaskFlow.toTask 4 CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Ok 60 @>

        [<Fact>]
        let ``TaskFlow delay reruns from scratch`` () =
            let runs = ref 0

            let workflow : TaskFlow<unit, string, int> =
                TaskFlow.delay(fun () ->
                    runs.Value <- runs.Value + 1
                    TaskFlow.succeed runs.Value)

            let runOnce () =
                workflow
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ runOnce () = Ok 1 @>
            test <@ runOnce () = Ok 2 @>

        [<Fact>]
        let ``shared combinators preserve task environment and error semantics`` () =
            let baseWorkflow : TaskFlow<int, int, int> =
                TaskFlow.read (fun env -> env + 1)
                |> TaskFlow.map ((*) 2)
                |> TaskFlow.bind (fun value -> TaskFlow.succeed(value + 3))
                |> TaskFlow.mapError String.length

            let workflow : TaskFlow<string, int, int> =
                TaskFlow.localEnv String.length baseWorkflow

            let result =
                workflow
                |> TaskFlow.run "flowkit" CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Ok 19 @>

        [<Fact>]
        let ``TaskFlow composition helpers cover error tapping fallback and pairing`` () =
            let tappedErrors = ResizeArray<string>()

            let tapPreservesOriginalError =
                TaskFlow.fail "primary"
                |> TaskFlow.tapError (fun error ->
                    tappedErrors.Add error
                    TaskFlow.succeed ())
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let tapSkipsSuccess =
                TaskFlow.succeed 42
                |> TaskFlow.tapError (fun error ->
                    tappedErrors.Add $"unexpected:{error}"
                    TaskFlow.succeed ())
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let recovered =
                TaskFlow.fail "missing"
                |> TaskFlow.orElse (TaskFlow.read (fun env -> env + 1))
                |> TaskFlow.run 41 CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let bypassesFallback =
                TaskFlow.succeed 10
                |> TaskFlow.orElse (TaskFlow.succeed 99)
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let zipped =
                TaskFlow.zip (TaskFlow.read (fun env -> env + 1)) (TaskFlow.read (fun env -> env * 2))
                |> TaskFlow.run 5 CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let mapped =
                TaskFlow.map2 (+) (TaskFlow.read (fun env -> env + 1)) (TaskFlow.read (fun env -> env * 2))
                |> TaskFlow.run 5 CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ tapPreservesOriginalError = Error "primary" @>
            test <@ tapSkipsSuccess = Ok 42 @>
            test <@ List.ofSeq tappedErrors = [ "primary" ] @>
            test <@ recovered = Ok 42 @>
            test <@ bypassesFallback = Ok 10 @>
            test <@ zipped = Ok(6, 10) @>
            test <@ mapped = Ok 16 @>

        [<Fact>]
        let ``Check bridges into flow, async, and task shapes`` () =
            let flowBridge =
                Check.okIf false
                |> Flow.orElseFlow (Flow.read (fun env -> $"flow:{env}"))
                |> Flow.run "env"

            let asyncBridge =
                Check.okIf false
                |> AsyncFlow.orElseAsync (async.Return "async")
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncFlowBridgeFromFlow =
                Check.okIf false
                |> AsyncFlow.orElseFlow (Flow.read (fun env -> $"async-flow:{env}"))
                |> AsyncFlow.run "env"
                |> Async.RunSynchronously

            let asyncFlowBridge =
                Check.okIf false
                |> AsyncFlow.orElseAsyncFlow (AsyncFlow.read (fun env -> $"async-flow:{env}"))
                |> AsyncFlow.run "env"
                |> Async.RunSynchronously

            let taskBridge =
                Check.okIf false
                |> TaskFlow.orElseTask (Task.FromResult "task")
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskAsyncBridge =
                Check.okIf false
                |> TaskFlow.orElseAsync (async.Return "task-async")
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskFlowBridge =
                Check.okIf false
                |> TaskFlow.orElseTaskFlow (TaskFlow.read (fun env -> $"task-flow:{env}"))
                |> TaskFlow.run "env" CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskAsyncFlowBridge =
                Check.okIf false
                |> TaskFlow.orElseAsyncFlow (AsyncFlow.read (fun env -> $"task-async-flow:{env}"))
                |> TaskFlow.run "env" CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let flowValue = Flow.value "flow-value" |> Flow.run ()
            let asyncValue = AsyncFlow.value "async-value" |> AsyncFlow.run () |> Async.RunSynchronously
            let taskValue =
                TaskFlow.value "task-value"
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ flowBridge = Error "flow:env" @>
            test <@ asyncBridge = Error "async" @>
            test <@ asyncFlowBridgeFromFlow = Error "async-flow:env" @>
            test <@ asyncFlowBridge = Error "async-flow:env" @>
            test <@ taskBridge = Error "task" @>
            test <@ taskAsyncBridge = Error "task-async" @>
            test <@ taskFlowBridge = Error "task-flow:env" @>
            test <@ taskAsyncFlowBridge = Error "task-async-flow:env" @>
            test <@ flowValue = Ok "flow-value" @>
            test <@ asyncValue = Ok "async-value" @>
            test <@ taskValue = Ok "task-value" @>

        [<Fact>]
        let ``AsyncFlow runtime helpers cover timeout retry and release`` () =
            let timeoutResult =
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 20.0)
                |> AsyncFlow.Runtime.timeout (TimeSpan.FromMilliseconds 1.0) "timed out"
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let retryRuns = ref 0

            let retryWorkflow =
                let policy : RetryPolicy<string> =
                    { MaxAttempts = 3
                      Delay = fun _ -> TimeSpan.Zero
                      ShouldRetry = fun error -> error = "transient" }

                AsyncFlow.delay(fun () ->
                    retryRuns.Value <- retryRuns.Value + 1

                    if retryRuns.Value < 2 then
                        AsyncFlow.fail "transient"
                    else
                        AsyncFlow.succeed 42)
                |> AsyncFlow.Runtime.retry policy

            let retryResult =
                retryWorkflow
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let releaseCount = ref 0

            let acquireReleaseResult =
                AsyncFlow.Runtime.useWithAcquireRelease
                    (AsyncFlow.succeed 7)
                    (fun _ _ ->
                        releaseCount.Value <- releaseCount.Value + 1
                        Task.CompletedTask)
                    (fun _ -> AsyncFlow.fail "boom")
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            test <@ timeoutResult = Error "timed out" @>
            test <@ retryResult = Ok 42 @>
            test <@ retryRuns.Value = 2 @>
            test <@ acquireReleaseResult = Error "boom" @>
            test <@ releaseCount.Value = 1 @>

        [<Fact>]
        let ``TaskFlow runtime helpers cover timeout retry and release`` () =
            let timeoutResult =
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 20.0)
                |> TaskFlow.Runtime.timeout (TimeSpan.FromMilliseconds 1.0) "timed out"
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let retryRuns = ref 0

            let retryWorkflow =
                let policy : RetryPolicy<string> =
                    { MaxAttempts = 3
                      Delay = fun _ -> TimeSpan.Zero
                      ShouldRetry = fun error -> error = "transient" }

                TaskFlow.delay(fun () ->
                    retryRuns.Value <- retryRuns.Value + 1

                    if retryRuns.Value < 2 then
                        TaskFlow.fail "transient"
                    else
                        TaskFlow.succeed 42)
                |> TaskFlow.Runtime.retry policy

            let retryResult =
                retryWorkflow
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let releaseCount = ref 0

            let acquireReleaseResult =
                TaskFlow.Runtime.useWithAcquireRelease
                    (TaskFlow.succeed 7)
                    (fun _ _ ->
                        releaseCount.Value <- releaseCount.Value + 1
                        Task.CompletedTask)
                    (fun _ -> TaskFlow.fail "boom")
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ timeoutResult = Error "timed out" @>
            test <@ retryResult = Ok 42 @>
            test <@ retryRuns.Value = 2 @>
            test <@ acquireReleaseResult = Error "boom" @>
            test <@ releaseCount.Value = 1 @>

        [<Fact>]
        let ``TaskFlow runtime context splits runtime services from app dependencies`` () =
            let runtime = { RuntimePrefix = "rt:"; Seen = ResizeArray() }

            let app =
                { DeviceClient =
                      { new IDeviceClient with
                          member _.Name = "client" }
                  Value = 41 }

            let context = RuntimeContext.create runtime app CancellationToken.None

            let workflow : TaskFlow<RuntimeContext<RuntimeServices, AppDependencies>, string, string> =
                taskFlow {
                    let! prefix = TaskFlow.readRuntime _.RuntimePrefix
                    let! value = TaskFlow.readEnvironment _.Value
                    runtime.Seen.Add $"value={value}"
                    return $"{prefix}{value}"
                }

            let result =
                workflow
                |> TaskFlow.runContext context
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Ok "rt:41" @>
            test <@ List.ofSeq runtime.Seen = [ "value=41" ] @>

        [<Fact>]
        let ``TaskFlowSpec runs a built workflow against the combined runtime context`` () =
            let runtime = { RuntimePrefix = "spec:"; Seen = ResizeArray() }

            let app =
                { DeviceClient =
                      { new IDeviceClient with
                          member _.Name = "spec-client" }
                  Value = 7 }

            let spec =
                TaskFlowSpec.create runtime app (fun () ->
                    taskFlow {
                        let! prefix = TaskFlow.readRuntime _.RuntimePrefix
                        let! value = TaskFlow.readEnvironment _.Value
                        return $"{prefix}{value}"
                    })

            let result =
                spec
                |> TaskFlowSpec.run CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Ok "spec:7" @>

        [<Fact>]
        let ``TaskFlow layers and capability helpers compose`` () =
            let runtime =
                { RuntimePrefix = "runtime:"
                  Seen = ResizeArray() }

            let app =
                { DeviceClient =
                      { new IDeviceClient with
                          member _.Name = "provider-client" }
                  Value = 10 }

            let outerContext = RuntimeContext.create runtime () CancellationToken.None

            let appLayer : TaskFlow<RuntimeContext<RuntimeServices, unit>, string, AppDependencies> =
                TaskFlow.succeed app

            let workflow : TaskFlow<AppDependencies, string, string> =
                taskFlow {
                    let! (client : IDeviceClient) =
                        Capability.service _.DeviceClient
                        : TaskFlow<AppDependencies, string, IDeviceClient>

                    let! value = TaskFlow.read _.Value
                    return $"{client.Name}:{value}"
                }

            let composed =
                workflow
                |> TaskFlow.provideLayer appLayer

            let composedResult =
                composed
                |> TaskFlow.runContext outerContext
                |> fun task -> task.GetAwaiter().GetResult()

            let provider = RecordingServiceProvider(typeof<IDeviceClient>, app.DeviceClient :> obj) :> IServiceProvider

            let providerResult =
                Capability.serviceFromProvider<IDeviceClient>
                |> TaskFlow.run provider CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let missingProviderResult =
                Capability.serviceFromProvider<IDeviceClient>
                |> TaskFlow.run (RecordingServiceProvider(typeof<string>, "nope") :> IServiceProvider) CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let flowCapability : Flow<AppDependencies, string, IDeviceClient> =
                Capability.service _.DeviceClient

            let asyncCapability : AsyncFlow<AppDependencies, string, IDeviceClient> =
                Capability.service _.DeviceClient

            let flowCapabilityResult =
                flowCapability
                |> Flow.run app

            let asyncCapabilityResult =
                asyncCapability
                |> AsyncFlow.run app
                |> Async.RunSynchronously

            let flowLayerWorkflow : Flow<AppDependencies, string, string> =
                flow {
                    let! client = Flow.read _.DeviceClient
                    let! value = Flow.read _.Value
                    return $"{client.Name}:{value}"
                }

            let flowLayerResult =
                flowLayerWorkflow
                |> Flow.provideLayer (Flow.succeed app)
                |> Flow.run ()

            let asyncLayerWorkflow : AsyncFlow<AppDependencies, string, string> =
                asyncFlow {
                    let! client = AsyncFlow.read _.DeviceClient
                    let! value = AsyncFlow.read _.Value
                    return $"{client.Name}:{value}"
                }

            let asyncLayerResult =
                asyncLayerWorkflow
                |> AsyncFlow.provideLayer (AsyncFlow.succeed app)
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            test <@ composedResult = Ok "provider-client:10" @>
            test <@ providerResult = Ok app.DeviceClient @>
            test <@ missingProviderResult = Error { CapabilityType = typeof<IDeviceClient> } @>
            test <@ flowCapabilityResult = Ok app.DeviceClient @>
            test <@ asyncCapabilityResult = Ok app.DeviceClient @>
            test <@ flowLayerResult = Ok "provider-client:10" @>
            test <@ asyncLayerResult = Ok "provider-client:10" @>

        [<Fact>]
        let ``Flow traverse and sequence work as expected`` () =
            let values = [ 1; 2; 3 ]
            let workflow = values |> Flow.traverse (fun v -> Flow.succeed (v * 2))
            let result = Flow.run () workflow
            test <@ result = Ok [ 2; 4; 6 ] @>

            let flows = [ Flow.succeed 1; Flow.succeed 2 ]
            let sequenceResult = Flow.run () (Flow.sequence flows)
            test <@ sequenceResult = Ok [ 1; 2 ] @>

            let failWorkflow = [ 1; 2 ] |> Flow.traverse (fun v -> if v = 1 then Flow.fail "error" else Flow.succeed v)
            test <@ Flow.run () failWorkflow = Error "error" @>

        [<Fact>]
        let ``AsyncFlow traverse and sequence work as expected`` () =
            let values = [ 1; 2; 3 ]
            let workflow = values |> AsyncFlow.traverse (fun v -> AsyncFlow.succeed (v * 2))
            let result = AsyncFlow.run () workflow |> Async.RunSynchronously
            test <@ result = Ok [ 2; 4; 6 ] @>

            let flows = [ AsyncFlow.succeed 1; AsyncFlow.succeed 2 ]
            let sequenceResult = AsyncFlow.run () (AsyncFlow.sequence flows) |> Async.RunSynchronously
            test <@ sequenceResult = Ok [ 1; 2 ] @>

        [<Fact>]
        let ``TaskFlow traverse and sequence work as expected`` () =
            let values = [ 1; 2; 3 ]
            let workflow = values |> TaskFlow.traverse (fun v -> TaskFlow.succeed (v * 2))
            let result = TaskFlow.run () CancellationToken.None workflow |> fun t -> t.GetAwaiter().GetResult()
            test <@ result = Ok [ 2; 4; 6 ] @>

            let flows = [ TaskFlow.succeed 1; TaskFlow.succeed 2 ]
            let sequenceResult = TaskFlow.run () CancellationToken.None (TaskFlow.sequence flows) |> fun t -> t.GetAwaiter().GetResult()
            test <@ sequenceResult = Ok [ 1; 2 ] @>

        [<Fact>]
        let ``AsyncFlow timeout helpers work as expected`` () =
            let okResult = 
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> AsyncFlow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
                |> AsyncFlow.run ()
                |> Async.RunSynchronously
            test <@ okResult = Ok () @>

            let errorResult =
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> AsyncFlow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
                |> AsyncFlow.run ()
                |> Async.RunSynchronously
            test <@ errorResult = Error "timed out" @>

            let withResult =
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> AsyncFlow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> AsyncFlow.succeed ())
                |> AsyncFlow.run ()
                |> Async.RunSynchronously
            test <@ withResult = Ok () @>

        [<Fact>]
        let ``TaskFlow timeout helpers work as expected`` () =
            let okResult = 
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> TaskFlow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
                |> TaskFlow.run () CancellationToken.None
                |> fun t -> t.GetAwaiter().GetResult()
            test <@ okResult = Ok () @>

            let errorResult =
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> TaskFlow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
                |> TaskFlow.run () CancellationToken.None
                |> fun t -> t.GetAwaiter().GetResult()
            test <@ errorResult = Error "timed out" @>

            let withResult =
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> TaskFlow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> TaskFlow.succeed ())
                |> TaskFlow.run () CancellationToken.None
                |> fun t -> t.GetAwaiter().GetResult()
            test <@ withResult = Ok () @>

        [<Fact>]
        let ``flow computation expression is sync only`` () =
            let workflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! doubled = Ok(env * 2)
                    return doubled
                }

            let publicMethods = publicInstanceMethodNames typeof<FlowBuilder>
            let argumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

            test <@ Flow.run 21 workflow = Ok 42 @>
            test <@ publicMethods |> Array.contains "Bind" @>
            test <@ publicMethods |> Array.contains "Yield" @>
            test <@ publicMethods |> Array.contains "YieldFrom" @>
            test <@ publicMethods |> Array.contains "ReturnFrom" @>
            test <@ argumentTypeNames = [| "FSharpFunc`2"; "FSharpOption`1"; "FSharpResult`2"; "FSharpValueOption`1"; "Flow`3" |] @>

        [<Fact>]
        let ``flow builders directly bind Result and Result unit values`` () =
            let syncWorkflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! doubled = Ok(env * 2)
                    do! Ok ()
                    return doubled
                }

            let asyncWorkflow : AsyncFlow<int, string, int> =
                asyncFlow {
                    let! env = AsyncFlow.env
                    let! doubled = Ok(env * 2)
                    do! Ok ()
                    return doubled
                }

            let taskWorkflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    let! doubled = Ok(env * 2)
                    do! Ok ()
                    return doubled
                }

            test <@ Flow.run 21 syncWorkflow = Ok 42 @>
            test <@ asyncWorkflow |> AsyncFlow.run 21 |> Async.RunSynchronously = Ok 42 @>
            test <@ taskWorkflow |> TaskFlow.run 21 CancellationToken.None |> fun task -> task.GetAwaiter().GetResult() = Ok 42 @>

        [<Fact>]
        let ``reader-style yield projects from the environment across builders`` () =
            let environment : ReaderEnv =
                { Prefix = "flow"
                  Count = 21 }

            let syncValue : Flow<ReaderEnv, string, int> =
                flow {
                    yield 42
                }

            let syncProjection : Flow<ReaderEnv, string, string> =
                flow {
                    yield _.Prefix
                }

            let syncYieldFrom : Flow<ReaderEnv, string, string> =
                flow {
                    yield! Flow.read _.Prefix
                }

            let asyncProjection : AsyncFlow<ReaderEnv, string, string> =
                asyncFlow {
                    yield _.Prefix
                }

            let asyncYieldFrom : AsyncFlow<ReaderEnv, string, string> =
                asyncFlow {
                    yield! AsyncFlow.read _.Prefix
                }

            let taskProjection : TaskFlow<ReaderEnv, string, string> =
                taskFlow {
                    yield _.Prefix
                }

            let taskYieldFrom : TaskFlow<ReaderEnv, string, string> =
                taskFlow {
                    yield! TaskFlow.read _.Prefix
                }

            test <@ Flow.run environment syncValue = Ok 42 @>
            test <@ Flow.run environment syncProjection = Ok "flow" @>
            test <@ Flow.run environment syncYieldFrom = Ok "flow" @>
            test <@ asyncProjection |> AsyncFlow.run environment |> Async.RunSynchronously = Ok "flow" @>
            test <@ asyncYieldFrom |> AsyncFlow.run environment |> Async.RunSynchronously = Ok "flow" @>
            test <@ taskProjection |> TaskFlow.run environment CancellationToken.None |> fun task -> task.GetAwaiter().GetResult() = Ok "flow" @>
            test <@ taskYieldFrom |> TaskFlow.run environment CancellationToken.None |> fun task -> task.GetAwaiter().GetResult() = Ok "flow" @>

        [<Fact>]
        let ``option and valueoption inputs short-circuit with unit errors across builders`` () =
            let syncSome : Flow<int, unit, int> =
                flow {
                    let! env = Flow.env
                    let! value = Some(env + 1)
                    return value * 2
                }

            let syncNone : Flow<int, unit, int> =
                flow {
                    let! env = Flow.env
                    let! value = None
                    return env + value
                }

            let syncValueSome : Flow<int, unit, int> =
                flow {
                    let! env = Flow.env
                    let! value = ValueSome(env + 1)
                    return value * 2
                }

            let syncValueNone : Flow<int, unit, int> =
                flow {
                    let! env = Flow.env
                    let! value = ValueNone
                    return env + value
                }

            let asyncWorkflow : AsyncFlow<int, unit, int> =
                asyncFlow {
                    let! env = AsyncFlow.env
                    let! value = Some(env + 1)
                    let! extra = ValueSome(value + 1)
                    return extra * 2
                }

            let asyncReturnFromNone : AsyncFlow<unit, unit, int> =
                asyncFlow { return! None }

            let taskWorkflow : TaskFlow<int, unit, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    let! value = Some(env + 1)
                    let! extra = ValueSome(value + 1)
                    return extra * 2
                }

            let taskReturnFromValueNone : TaskFlow<unit, unit, int> =
                taskFlow { return! ValueNone }

            let asyncArgumentTypeNames = asyncFlowBuilderBindAndReturnFromArgumentNames ()
            let taskArgumentTypeNames = taskFlowBuilderBindAndReturnFromArgumentNames ()

            test <@ Flow.run 20 syncSome = Ok 42 @>
            test <@ Flow.run 20 syncNone = Error() @>
            test <@ Flow.run 20 syncValueSome = Ok 42 @>
            test <@ Flow.run 20 syncValueNone = Error() @>
            test <@ asyncWorkflow |> AsyncFlow.run 19 |> Async.RunSynchronously = Ok 42 @>
            test <@ asyncReturnFromNone |> AsyncFlow.run () |> Async.RunSynchronously = Error() @>
            test <@ taskWorkflow |> TaskFlow.run 19 CancellationToken.None |> fun task -> task.GetAwaiter().GetResult() = Ok 42 @>
            test <@ taskReturnFromValueNone |> TaskFlow.run () CancellationToken.None |> fun task -> task.GetAwaiter().GetResult() = Error() @>
            test <@ asyncArgumentTypeNames |> Array.contains "FSharpOption`1" @>
            test <@ asyncArgumentTypeNames |> Array.contains "FSharpResult`2" @>
            test <@ asyncArgumentTypeNames |> Array.contains "FSharpValueOption`1" @>
            test <@ taskArgumentTypeNames |> Array.contains "FSharpOption`1" @>
            test <@ taskArgumentTypeNames |> Array.contains "FSharpResult`2" @>
            test <@ taskArgumentTypeNames |> Array.contains "FSharpValueOption`1" @>

        [<Fact>]
        let ``option and valueoption implicit binding requires unit workflow errors`` () =
            let fsFlowAssemblyPath = typeof<FlowBuilder>.Assembly.Location
            let fsFlowNetAssemblyPath = typeof<TaskFlowBuilder>.Assembly.Location

            let flowProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    open FsFlow

    let probe : Flow<unit, string, int> =
        flow {{
            let! value = Some 42
            return value
        }}
    """

            let asyncProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    open FsFlow

    let probe : AsyncFlow<unit, string, int> =
        asyncFlow {{
            let! value = ValueSome 42
            return value
        }}
    """

            let taskProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    #r @"{fsFlowNetAssemblyPath}"
    open FsFlow
    open FsFlow

    let probe : TaskFlow<unit, string, int> =
        taskFlow {{
            let! value = Some 42
            return value
        }}
    """

            let flowExitCode, flowOutput = runFsiScript flowProbe
            let asyncExitCode, asyncOutput = runFsiScript asyncProbe
            let taskExitCode, taskOutput = runFsiScript taskProbe

            test <@ flowExitCode <> 0 @>
            test <@ flowOutput.Contains("Flow<unit,unit,int>") @>
            test <@ asyncExitCode <> 0 @>
            test <@ asyncOutput.Contains("AsyncFlow<unit,unit,int>") @>
            test <@ taskExitCode <> 0 @>
            test <@ taskOutput.Contains("TaskFlow<unit,unit,int>") @>

        [<Fact>]
        let ``explicit option adapters support custom workflow errors across modules`` () =
            let syncSome =
                Some 21
                |> Flow.fromOption "missing value"
                |> Flow.map ((*) 2)
                |> Flow.run ()

            let syncNone =
                None
                |> Flow.fromOption "missing value"
                |> Flow.run ()

            let syncValueSome =
                ValueSome 21
                |> Flow.fromValueOption "missing value"
                |> Flow.map ((*) 2)
                |> Flow.run ()

            let syncValueNone =
                ValueNone
                |> Flow.fromValueOption "missing value"
                |> Flow.run ()

            let asyncSome =
                Some 21
                |> AsyncFlow.fromOption "missing value"
                |> AsyncFlow.map ((*) 2)
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncNone =
                None
                |> AsyncFlow.fromOption "missing value"
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncValueSome =
                ValueSome 21
                |> AsyncFlow.fromValueOption "missing value"
                |> AsyncFlow.map ((*) 2)
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let asyncValueNone =
                ValueNone
                |> AsyncFlow.fromValueOption "missing value"
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let taskSome =
                Some 21
                |> TaskFlow.fromOption "missing value"
                |> TaskFlow.map ((*) 2)
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskNone =
                None
                |> TaskFlow.fromOption "missing value"
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskValueSome =
                ValueSome 21
                |> TaskFlow.fromValueOption "missing value"
                |> TaskFlow.map ((*) 2)
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let taskValueNone =
                ValueNone
                |> TaskFlow.fromValueOption "missing value"
                |> TaskFlow.run () CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ syncSome = Ok 42 @>
            test <@ syncNone = Error "missing value" @>
            test <@ syncValueSome = Ok 42 @>
            test <@ syncValueNone = Error "missing value" @>
            test <@ asyncSome = Ok 42 @>
            test <@ asyncNone = Error "missing value" @>
            test <@ asyncValueSome = Ok 42 @>
            test <@ asyncValueNone = Error "missing value" @>
            test <@ taskSome = Ok 42 @>
            test <@ taskNone = Error "missing value" @>
            test <@ taskValueSome = Ok 42 @>
            test <@ taskValueNone = Error "missing value" @>

        [<Fact>]
        let ``flow computation expression rejects task-oriented binds when task helpers are imported`` () =
            let fsFlowAssemblyPath = typeof<FlowBuilder>.Assembly.Location
            let fsFlowNetAssemblyPath = typeof<TaskFlowBuilder>.Assembly.Location

            let taskProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    #r @"{fsFlowNetAssemblyPath}"
    open System.Threading.Tasks
    open FsFlow
    open FsFlow

    let probe : Flow<unit, string, int> =
        flow {{
            let! value = Task.FromResult 42
            return value
        }}
    """

            let taskFlowProbe =
                $"""
    #r @"{fsFlowAssemblyPath}"
    #r @"{fsFlowNetAssemblyPath}"
    open FsFlow
    open FsFlow

    let probe : Flow<unit, string, int> =
        flow {{
            let! value = TaskFlow.succeed 42
            return value
        }}
    """

            let taskExitCode, taskOutput = runFsiScript taskProbe
            let taskFlowExitCode, taskFlowOutput = runFsiScript taskFlowProbe

            test <@ taskExitCode <> 0 @>
            test <@ taskOutput.Contains("Bind") @>
            test <@ taskFlowExitCode <> 0 @>
            test <@ taskFlowOutput.Contains("Bind") @>

        [<Fact>]
        let ``asyncFlow lives in FsFlow and composes sync flows`` () =
            let workflow : AsyncFlow<int, string, int> =
                asyncFlow {
                    let! env = Flow.env
                    let! baseValue = AsyncFlow.succeed(env + 1)
                    return baseValue * 2
                }

            let result =
                workflow
                |> AsyncFlow.run 20
                |> Async.RunSynchronously

            test <@ typeof<AsyncFlowBuilder>.Namespace = "FsFlow" @>
            test <@ result = Ok 42 @>

        [<Fact>]
        let ``asyncFlow directly binds and returns Async and Async Result values`` () =
            let workflow : AsyncFlow<int, string, int> =
                asyncFlow {
                    let! env = AsyncFlow.env
                    let! baseValue = async { return env + 1 }
                    let! (adjustedValue : int) = async { return Ok(baseValue * 2) }
                    return adjustedValue + 2
                }

            let asyncReturnFrom : AsyncFlow<unit, string, int> =
                asyncFlow { return! async { return 42 } }

            let workflowResult =
                workflow
                |> AsyncFlow.run 19
                |> Async.RunSynchronously

            let asyncReturnFromResult =
                asyncReturnFrom
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            test <@ workflowResult = Ok 42 @>
            test <@ asyncReturnFromResult = Ok 42 @>
            test <@ hasAsyncResultReturnFromOverload typeof<AsyncFlowBuilder> @>

        [<Fact>]
        let ``taskFlow lives in FsFlow and composes async flows`` () =
            let workflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = Flow.env
                    let! baseValue = AsyncFlow.succeed(env + 1)
                    return baseValue * 2
                }

            let result =
                workflow
                |> TaskFlow.run 20 CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ typeof<TaskFlowBuilder>.Namespace = "FsFlow" @>
            test <@ result = Ok 42 @>

        [<Fact>]
        let ``taskFlow directly binds and returns Async, Task, and result-bearing values`` () =
            let resultTask (value: int) : Task<Result<int, string>> = Task.FromResult(Ok value)

            let workflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    do! Task.CompletedTask
                    let! baseValue = async { return env + 1 }
                    let! adjustedValue = async { return Ok(baseValue * 2) }
                    return adjustedValue + 2
                }

            let asyncReturnFrom : TaskFlow<unit, string, int> =
                taskFlow { return! async { return 42 } }

            let taskWorkflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    do! Task.CompletedTask
                    let! baseValue = Task.FromResult(env + 1)
                    let! (adjustedValue : int) = resultTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let taskReturnFrom : TaskFlow<unit, string, unit> =
                taskFlow { return! Task.CompletedTask }

            let taskReturnFromResult : TaskFlow<unit, string, int> =
                taskFlow { return! resultTask 42 }

            let run flow environment =
                flow
                |> TaskFlow.run environment CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let workflowResult = run workflow 19
            let asyncReturnFromResult = run asyncReturnFrom ()
            let taskWorkflowResult = run taskWorkflow 19
            let taskReturnFromUnitResult = run taskReturnFrom ()
            let taskReturnFromResultResult = run taskReturnFromResult ()

            test <@ workflowResult = Ok 42 @>
            test <@ asyncReturnFromResult = Ok 42 @>
            test <@ taskWorkflowResult = Ok 42 @>
            test <@ taskReturnFromUnitResult = Ok() @>
            test <@ taskReturnFromResultResult = Ok 42 @>
            test <@ hasAsyncResultReturnFromOverload typeof<TaskFlowBuilder> @>

        [<Fact>]
        let ``taskFlow directly binds and returns ValueTask and result-bearing ValueTask values`` () =
            let resultValueTask (value: int) : ValueTask<Result<int, string>> = ValueTask<Result<int, string>>(Ok value)

            let workflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    do! ValueTask()
                    let! baseValue = ValueTask<int>(env + 1)
                    let! (adjustedValue : int) = resultValueTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let valueTaskReturnFrom : TaskFlow<unit, string, unit> =
                taskFlow { return! ValueTask() }

            let valueTaskReturnFromValue : TaskFlow<unit, string, int> =
                taskFlow { return! ValueTask<int>(42) }

            let valueTaskReturnFromResult : TaskFlow<unit, string, int> =
                taskFlow { return! resultValueTask 42 }

            let run flow environment =
                flow
                |> TaskFlow.run environment CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            let workflowResult = run workflow 19
            let valueTaskReturnFromUnitResult = run valueTaskReturnFrom ()
            let valueTaskReturnFromValueResult = run valueTaskReturnFromValue ()
            let valueTaskReturnFromResultResult = run valueTaskReturnFromResult ()

            test <@ workflowResult = Ok 42 @>
            test <@ valueTaskReturnFromUnitResult = Ok() @>
            test <@ valueTaskReturnFromValueResult = Ok 42 @>
            test <@ valueTaskReturnFromResultResult = Ok 42 @>

        [<Fact>]
        let ``TaskFlow keeps a Task-backed execution backbone even when lifting ValueTask inputs`` () =
            let workflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    let! value = ValueTask<int>(env + 1)
                    return value * 2
                }

            let runningTask = TaskFlow.run 20 CancellationToken.None workflow
            let result = runningTask.GetAwaiter().GetResult()

            test <@ runningTask.GetType() = typeof<Task<Result<int, string>>> @>
            test <@ result = Ok 42 @>

        [<Fact>]
        let ``taskFlow directly binds and returns ColdTask values`` () =
            let seen = ref CancellationToken.None
            use cts = new CancellationTokenSource()

            let resultColdTask (value: int) : ColdTask<Result<int, string>> =
                ColdTask(fun cancellationToken ->
                    seen.Value <- cancellationToken
                    Task.FromResult(Ok value))

            let workflow : TaskFlow<int, string, int> =
                taskFlow {
                    let! env = TaskFlow.env
                    let! baseValue =
                        ColdTask(fun cancellationToken ->
                            seen.Value <- cancellationToken
                            Task.FromResult(env + 1))

                    let! (adjustedValue : int) = resultColdTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let coldTaskReturnFromValue : TaskFlow<unit, string, int> =
                taskFlow { return! ColdTask(fun _ -> Task.FromResult 42) }

            let coldTaskReturnFromResult : TaskFlow<unit, string, int> =
                taskFlow { return! resultColdTask 42 }

            let run flow environment cancellationToken =
                flow
                |> TaskFlow.run environment cancellationToken
                |> fun task -> task.GetAwaiter().GetResult()

            let workflowResult = run workflow 19 cts.Token
            let coldTaskReturnFromValueResult = run coldTaskReturnFromValue () cts.Token
            let coldTaskReturnFromResultResult = run coldTaskReturnFromResult () cts.Token

            test <@ workflowResult = Ok 42 @>
            test <@ coldTaskReturnFromValueResult = Ok 42 @>
            test <@ coldTaskReturnFromResultResult = Ok 42 @>
            test <@ seen.Value = cts.Token @>

        [<Fact>]
        let ``asyncFlow directly binds and returns Task values when task helpers are imported`` () =
            let resultTask (value: int) : Task<Result<int, string>> = Task.FromResult(Ok value)

            let workflow : AsyncFlow<int, string, int> =
                asyncFlow {
                    let! env = AsyncFlow.env
                    do! Task.CompletedTask
                    let! baseValue = Task.FromResult(env + 1)
                    let! (adjustedValue : int) = resultTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let taskReturnFrom : AsyncFlow<unit, string, unit> =
                asyncFlow { return! Task.CompletedTask }

            let taskReturnFromResult : AsyncFlow<unit, string, int> =
                asyncFlow {
                    let! (value : int) = resultTask 42
                    return value
                }

            let workflowResult =
                workflow
                |> AsyncFlow.run 19
                |> Async.RunSynchronously

            let taskReturnFromUnitResult =
                taskReturnFrom
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let taskReturnFromResultResult =
                taskReturnFromResult
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            test <@ workflowResult = Ok 42 @>
            test <@ taskReturnFromUnitResult = Ok() @>
            test <@ taskReturnFromResultResult = Ok 42 @>

        [<Fact>]
        let ``asyncFlow directly binds and returns ValueTask values when task helpers are imported`` () =
            let resultValueTask (value: int) : ValueTask<Result<int, string>> = ValueTask<Result<int, string>>(Ok value)

            let workflow : AsyncFlow<int, string, int> =
                asyncFlow {
                    let! env = AsyncFlow.env
                    do! ValueTask()
                    let! baseValue = ValueTask<int>(env + 1)
                    let! (adjustedValue : int) = resultValueTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let valueTaskReturnFrom : AsyncFlow<unit, string, unit> =
                asyncFlow { return! ValueTask() }

            let valueTaskReturnFromValue : AsyncFlow<unit, string, int> =
                asyncFlow { return! ValueTask<int>(42) }

            let valueTaskReturnFromResult : AsyncFlow<unit, string, int> =
                asyncFlow {
                    let! (value : int) = resultValueTask 42
                    return value
                }

            let workflowResult =
                workflow
                |> AsyncFlow.run 19
                |> Async.RunSynchronously

            let valueTaskReturnFromUnitResult =
                valueTaskReturnFrom
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let valueTaskReturnFromValueResult =
                valueTaskReturnFromValue
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            let valueTaskReturnFromResultResult =
                valueTaskReturnFromResult
                |> AsyncFlow.run ()
                |> Async.RunSynchronously

            test <@ workflowResult = Ok 42 @>
            test <@ valueTaskReturnFromUnitResult = Ok() @>
            test <@ valueTaskReturnFromValueResult = Ok 42 @>
            test <@ valueTaskReturnFromResultResult = Ok 42 @>

        [<Fact>]
        let ``asyncFlow directly binds and returns ColdTask values when task helpers are imported`` () =
            let seen = ref CancellationToken.None
            use cts = new CancellationTokenSource()

            let resultColdTask (value: int) : ColdTask<Result<int, string>> =
                ColdTask(fun cancellationToken ->
                    seen.Value <- cancellationToken
                    Task.FromResult(Ok value))

            let workflow : AsyncFlow<int, string, int> =
                asyncFlow {
                    let! env = AsyncFlow.env
                    let! baseValue =
                        ColdTask(fun cancellationToken ->
                            seen.Value <- cancellationToken
                            Task.FromResult(env + 1))

                    let! (adjustedValue : int) = resultColdTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let coldTaskReturnFromValue : AsyncFlow<unit, string, int> =
                asyncFlow { return! ColdTask(fun _ -> Task.FromResult 42) }

            let coldTaskReturnFromResult : AsyncFlow<unit, string, int> =
                asyncFlow {
                    let! (value : int) = resultColdTask 42
                    return value
                }

            let workflowResult =
                workflow
                |> AsyncFlow.run 19
                |> fun operation -> Async.StartAsTask(operation, cancellationToken = cts.Token)
                |> fun task -> task.GetAwaiter().GetResult()

            let coldTaskReturnFromValueResult =
                coldTaskReturnFromValue
                |> AsyncFlow.run ()
                |> fun operation -> Async.StartAsTask(operation, cancellationToken = cts.Token)
                |> fun task -> task.GetAwaiter().GetResult()

            let coldTaskReturnFromResultResult =
                coldTaskReturnFromResult
                |> AsyncFlow.run ()
                |> fun operation -> Async.StartAsTask(operation, cancellationToken = cts.Token)
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ workflowResult = Ok 42 @>
            test <@ coldTaskReturnFromValueResult = Ok 42 @>
            test <@ coldTaskReturnFromResultResult = Ok 42 @>
            test <@ seen.Value = cts.Token @>

        [<Fact>]
        let ``Guard constructors work in all flow families`` () =
            let successOption : int option = Some 42
            let successValueOption : int voption = ValueSome 10
            let asyncOption : Async<int option> = async { return Some 42 }
            let asyncValueOption : Async<int voption> = async { return ValueSome 10 }
            let asyncBool : Async<bool> = async { return true }
            let successTaskOption : Task<int option> = Task.FromResult(Some 5)
            let successTaskValueOption : ValueTask<int voption> = ValueTask.FromResult(ValueSome 3)
            let guardedSuccessOption : Result<int, string> = Guard.Of("missing-option", successOption)
            let guardedSuccessValueOption : Result<int, string> = Guard.Of("missing-voption", successValueOption)
            let guardedBool : Result<unit, string> = Guard.Of("bool-false", true)
            let guardedAsyncOption : Async<Result<int, string>> = Guard.Of("missing-option", asyncOption)
            let guardedAsyncValueOption : Async<Result<int, string>> = Guard.Of("missing-voption", asyncValueOption)
            let guardedAsyncBool : Async<Result<unit, string>> = Guard.Of("bool-false", asyncBool)
            let guardedTaskOption : Task<Result<int, string>> = Guard.Of("task-missing", successTaskOption)
            let guardedTaskValueOption : ValueTask<Result<int, string>> = Guard.Of("vtask-missing", successTaskValueOption)

            let flowTest =
                flow {
                    let! x = guardedSuccessOption
                    let! y = guardedSuccessValueOption
                    do! guardedBool
                    return x + y
                }

            let asyncFlowTest =
                asyncFlow {
                    let! (x : int) = guardedAsyncOption
                    let! (y : int) = guardedAsyncValueOption
                    do! guardedAsyncBool
                    return x + y
                }

            let taskFlowTest =
                taskFlow {
                    let! x = guardedSuccessOption
                    let! y = guardedSuccessValueOption
                    do! guardedBool
                    let! z = guardedTaskOption
                    let! w = guardedTaskValueOption
                    return x + y + z + w
                }

            let flowResult = Flow.run () flowTest
            let asyncFlowResult = AsyncFlow.run () asyncFlowTest |> Async.RunSynchronously
            let taskFlowResult = TaskFlow.run () CancellationToken.None taskFlowTest |> fun t -> t.GetAwaiter().GetResult()

            test <@ flowResult = Ok 52 @>
            test <@ asyncFlowResult = Ok 52 @>
            test <@ taskFlowResult = Ok 60 @>

        [<Fact>]
        let ``AsyncFlow login syntax uses Guard constructors and error mapping`` () =
            let tryGetUser username = async { return if username = "missing" then None else Some username }
            let isPwdValid password user = password = $"{user}-pwd"
            let authorize user = async { return if user = "blocked" then Error "denied" else Ok () }
            let createAuthToken user = if user = "expired" then Error "token-expired" else Ok $"token-{user}"

            let login username password =
                asyncFlow {
                    let userResult : Async<Result<string, LoginError>> = Guard.Of(InvalidUser, tryGetUser username)
                    let! (user : string) = userResult

                    let passwordCheck : Result<unit, LoginError> = Guard.Of(InvalidPwd, isPwdValid password user)
                    do! passwordCheck

                    let authorizeResult : Async<Result<unit, LoginError>> = Guard.MapError(Unauthorized, authorize user)
                    do! authorizeResult

                    let tokenResult : Result<string, LoginError> = Guard.MapError(TokenErr, createAuthToken user)
                    return! tokenResult
                }

            let success = AsyncFlow.run () (login "alice" "alice-pwd") |> Async.RunSynchronously
            let authFailure = AsyncFlow.run () (login "blocked" "blocked-pwd") |> Async.RunSynchronously
            let tokenFailure = AsyncFlow.run () (login "expired" "expired-pwd") |> Async.RunSynchronously

            test <@ success = Ok "token-alice" @>
            test <@ authFailure = Error (Unauthorized "denied") @>
            test <@ tokenFailure = Error (TokenErr "token-expired") @>

        [<Fact>]
        let ``Guard mapError stays symmetric across flow families`` () =
            let asyncSource : AsyncFlow<unit, string, int> = AsyncFlow.fail "async-source"
            let taskSource : TaskFlow<unit, string, int> = TaskFlow.fail "task-source"
            let asyncSuccess : AsyncFlow<unit, string, int> = AsyncFlow.succeed 1

            let asyncMapped =
                let mappedAsyncSource : AsyncFlow<unit, string, int> =
                    Guard.MapError((fun error -> $"mapped-{error}"), asyncSource)

                asyncFlow {
                    let! value = mappedAsyncSource
                    return value + 1
                }

            let taskMapped =
                let mappedAsyncSuccess : AsyncFlow<unit, string, int> =
                    Guard.MapError((fun error -> $"mapped-{error}"), asyncSuccess)

                let mappedTaskSource : TaskFlow<unit, string, int> =
                    Guard.MapError((fun error -> $"mapped-{error}"), taskSource)

                taskFlow {
                    let! asyncValue = mappedAsyncSuccess
                    let! taskValue = mappedTaskSource
                    return asyncValue + taskValue
                }

            test <@ AsyncFlow.run () asyncMapped |> Async.RunSynchronously = Error "mapped-async-source" @>
            test <@ TaskFlow.run () CancellationToken.None taskMapped |> fun t -> t.GetAwaiter().GetResult() = Error "mapped-task-source" @>

        [<Fact>]
        let ``Guard.of fails correctly for check-like sources`` () =
            let missingOption : int option = None
            let guardedFlowFail : Result<int, string> = Guard.Of("failed", missingOption)
            let guardedAsyncFlowFail : Async<Result<int, string>> = Guard.Of("failed", async { return ValueNone })
            let guardedTaskFlowFail : Result<int, string> = Guard.Of("failed", missingOption)

            let flowFail = flow {
                let! (value : int) = guardedFlowFail
                return value
            }
            let asyncFlowFail = asyncFlow {
                let! (value : int) = guardedAsyncFlowFail
                return value
            }
            let taskFlowFail = taskFlow {
                let! (value : int) = guardedTaskFlowFail
                return value
            }

            test <@ Flow.run () flowFail = Error "failed" @>
            test <@ AsyncFlow.run () asyncFlowFail |> Async.RunSynchronously = Error "failed" @>
            test <@ TaskFlow.run () CancellationToken.None taskFlowFail |> fun t -> t.GetAwaiter().GetResult() = Error "failed" @>
