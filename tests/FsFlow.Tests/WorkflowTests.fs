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
        let ``Fiber: fork and join success`` () =
            let workflow : Flow<unit, string, int> =
                flow {
                    let! (fiber: Fiber<string, int>) = Flow.ok 42 |> Flow.fork
                    let! result = fiber |> Flow.join
                    return result
                }

            test <@ Flow.run () workflow = Exit.Success 42 @>

        [<Fact>]
        let ``Fiber: fork and join failure`` () =
            let workflow : Flow<unit, string, int> =
                flow {
                    let! (fiber: Fiber<string, int>) = Flow.fail "boom" |> Flow.fork
                    let! result = fiber |> Flow.join
                    return result
                }

            test <@ Flow.run () workflow = Exit.Failure (Cause.Fail "boom") @>

        [<Fact>]
        let ``Fiber: interrupt stops execution`` () =
            let mutable executed = false
            let workflow =
                flow {
                    let! (fiber: Fiber<string, int>) = 
                        flow {
                            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 500.0)
                            executed <- true
                            return 42
                        }
                        |> Flow.fork
                    
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                    let! exit = fiber |> Flow.interrupt
                    return exit
                }

            let outcome = Flow.run () workflow
            
            match outcome with
            | Exit.Success (Exit.Failure Cause.Interrupt) -> 
                test <@ executed = false @>
            | _ -> failwithf "Expected interrupted exit, got %A" outcome

        [<Fact>]
        let ``Flow: zipPar combines results concurrently`` () =
            let workflow =
                Flow.zipPar
                    (flow { 
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                        return 1 
                    })
                    (flow { 
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                        return 2 
                    })

            test <@ Flow.run () workflow = Exit.Success (1, 2) @>

        [<Fact>]
        let ``Flow: zipPar interrupts on failure`` () =
            let mutable executed = false
            let workflow =
                Flow.zipPar
                    (flow { 
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 500.0)
                        executed <- true
                        return 1 
                    })
                    (Flow.fail "boom")

            let outcome = Flow.run () workflow
            test <@ outcome = Exit.Failure (Cause.Fail "boom") @>
            test <@ executed = false @>

        [<Fact>]
        let ``Flow: race returns first result and interrupts loser`` () =
            let mutable loserExecuted = false
            let workflow =
                Flow.race
                    (flow { 
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                        return 1 
                    })
                    (flow { 
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 500.0)
                        loserExecuted <- true
                        return 2 
                    })

            test <@ Flow.run () workflow = Exit.Success 1 @>
            // Give it a bit more time to potentially execute (though it shouldn't)
            Thread.Sleep(500)
            test <@ loserExecuted = false @>

        [<Fact>]
        let ``Ref: atomic updates`` () =
            let workflow =
                flow {
                    let! r = Ref.make 10
                    do! r |> Ref.update (fun v -> v + 1)
                    let! v1 = r |> Ref.get
                    let! v2 = r |> Ref.modify (fun v -> v * 2, "result")
                    let! v3 = r |> Ref.get
                    return v1, v2, v3
                }

            test <@ Flow.run () workflow = Exit.Success (11, "result", 22) @>

        [<Fact>]
        let ``STM: atomic transactional updates`` () =
            let workflow =
                flow {
                    let! r1 = TRef.make 10 |> STM.atomically
                    let! r2 = TRef.make 20 |> STM.atomically
                    
                    let tx = 
                        stm {
                            let! v1 = TRef.get r1
                            let! v2 = TRef.get r2
                            do! TRef.set (v1 + 5) r1
                            do! TRef.set (v2 - 5) r2
                        }
                    do! tx |> STM.atomically
                    
                    let! v1 = TRef.get r1 |> STM.atomically
                    let! v2 = TRef.get r2 |> STM.atomically
                    return v1, v2
                }

            test <@ Flow.run () workflow = Exit.Success (15, 15) @>

        [<Fact>]
        let ``FlowStream: consumes sequence correctly`` () =
            let mutable sum = 0
            let stream = FlowStream.fromSeq [1; 2; 3; 4; 5]
            let workflow = 
                stream 
                |> FlowStream.map (fun v -> v * 2)
                |> FlowStream.runForEach () (fun v -> sum <- sum + v)

            let result = Flow.run () workflow
            test <@ result = Exit.Success () @>
            test <@ sum = 30 @>

        type private DeviceClient(name: string) =
            interface IDeviceClient with
                member _.Name = name

        type private ProjectionService() =
            member _.Number = 21
            member _.NumberResult : Result<int, string> = Ok 21
            member _.MaybeNumber : int option = Some 21
            member _.MaybeValueNumber : int voption = ValueSome 21
            member _.FlowNumber : Flow<ProjectionService, string, int> = Flow.ok 21
            member _.AsyncNumber : Async<int> = async { return 21 }
            member _.AsyncResultNumber : Async<Result<int, string>> = async { return Ok 21 }
            member _.TaskNumber : Task<int> = Task.FromResult 21
            member _.TaskResultNumber : Task<Result<int, string>> = Task.FromResult(Ok 21)
            member _.ValueTaskNumber : ValueTask<int> = ValueTask<int>(21)
            member _.ValueTaskResultNumber : ValueTask<Result<int, string>> = ValueTask<Result<int, string>>(Ok 21)
            member _.ColdTaskNumber : ColdTask<int> = ColdTask(fun _ -> Task.FromResult 21)
            member _.ColdTaskResultNumber : ColdTask<Result<int, string>> = ColdTask(fun _ -> Task.FromResult(Ok 21))
            member _.ColdTaskUnit : ColdTask<unit> = ColdTask(fun _ -> Task.FromResult ())

        type private CountingCaps() =
            let accessCount = ref 0

            member _.AccessCount = accessCount.Value

            interface Needs<IDeviceClient> with
                member _.Dep =
                    accessCount.Value <- accessCount.Value + 1
                    DeviceClient($"dep-{accessCount.Value}") :> IDeviceClient

        type private ProjectionCaps() =
            let service = ProjectionService()

            interface Needs<ProjectionService> with
                member _.Dep = service

        let private assertEnvRequestDoesNotCompile
            (workflowTypeName: string)
            (builderName: string) =
            let assemblyPath = typeof<FlowBuilder>.Assembly.Location

            let script =
                $"""
#r @"{assemblyPath}"
open FsFlow

type WrongEnv =
    {{ DeviceClient: string }}

let request : Env<string> = Unchecked.defaultof<_>

let probe : {workflowTypeName}<WrongEnv, string, string> =

    {builderName} {{
        do! request
        let! (value: string) = request
        return value
    }}
"""

            let exitCode, output = runFsiScript script

            test <@ exitCode <> 0 @>
            test <@ not (String.IsNullOrWhiteSpace output) @>

        [<Fact>]
        let ``whole dependency Env requests stay cold across flow families`` () =
            let flowCaps = CountingCaps()
            let asyncCaps = CountingCaps()
            let taskCaps = CountingCaps()
            let request : Env<IDeviceClient> = Unchecked.defaultof<_>

            let flowWorkflow : Flow<CountingCaps, string, string> =
                flow {
                    do! request
                    let! (client: IDeviceClient) = request
                    return client.Name
                }

            let asyncWorkflow : Flow<CountingCaps, string, string> =
                flow {
                    do! request
                    let! (client: IDeviceClient) = request
                    return client.Name
                }

            let taskWorkflow : Flow<CountingCaps, string, string> =
                flow {
                    do! request
                    let! (client: IDeviceClient) = request
                    return client.Name
                }

            let flowRun1 = Flow.run flowCaps flowWorkflow
            let flowRun2 = Flow.run flowCaps flowWorkflow
            let asyncRun1 = Flow.run asyncCaps asyncWorkflow
            let asyncRun2 = Flow.run asyncCaps asyncWorkflow
            let taskRun1 = Flow.run taskCaps taskWorkflow

            let taskRun2 = Flow.run taskCaps taskWorkflow

            test <@ flowRun1 = Exit.Success "dep-2" @>
            test <@ flowRun2 = Exit.Success "dep-4" @>
            test <@ flowCaps.AccessCount = 4 @>
            test <@ asyncRun1 = Exit.Success "dep-2" @>
            test <@ asyncRun2 = Exit.Success "dep-4" @>
            test <@ asyncCaps.AccessCount = 4 @>
            test <@ taskRun1 = Exit.Success "dep-2" @>
            test <@ taskRun2 = Exit.Success "dep-4" @>
            test <@ taskCaps.AccessCount = 4 @>

        [<Fact>]
        let ``whole dependency Env requests fail without Needs on flow`` () =
            assertEnvRequestDoesNotCompile "Flow" "flow"

        [<Fact>]
        let ``whole dependency Env requests fail without Needs on asyncFlow`` () =
            assertEnvRequestDoesNotCompile "AsyncFlow" "asyncFlow"

        [<Fact>]
        let ``whole dependency Env requests fail without Needs on taskFlow`` () =
            assertEnvRequestDoesNotCompile "TaskFlow" "taskFlow"

        [<Fact>]
        let ``projected Env requests bind through flow and asyncFlow core shapes`` () =
            let environment = ProjectionCaps()
            let plainRequest : Env<ProjectionService, int> = Env (fun service -> service.Number)
            let resultUnitRequest : Env<ProjectionService, Result<int, unit>> = Env (fun service -> Ok service.Number)
            let maybeRequest : Env<ProjectionService, int option> = Env (fun service -> service.MaybeNumber)
            let maybeValueRequest : Env<ProjectionService, int voption> = Env (fun service -> service.MaybeValueNumber)

            let flowWorkflow : Flow<ProjectionCaps, unit, int> =
                flow {
                    let! (plain : int) = plainRequest
                    let! (resultValue : int) = resultUnitRequest
                    let! (maybeValue : int) = maybeRequest
                    let! (maybeValueOption : int) = maybeValueRequest
                    return plain + resultValue + maybeValue + maybeValueOption
                }

            test <@ Flow.run environment flowWorkflow = Exit.Success 84 @>

        [<Fact>]
        let ``projected Env requests bind async result shapes`` () =
            let environment = ProjectionCaps()
            let asyncResultRequest : Env<ProjectionService, Async<Result<int, string>>> =
                Env (fun service -> service.AsyncResultNumber)

            let asyncWorkflow : Flow<ProjectionCaps, string, int> =
                flow {
                    let! (asyncResultValue : int) = asyncResultRequest
                    return asyncResultValue
                }

            test <@ Flow.run environment asyncWorkflow = Exit.Success 21 @>

        [<Fact>]
        let ``projected Env requests bind task surfaces across flow`` () =
            let environment = ProjectionCaps()
            let plainRequest : Env<ProjectionService, int> = Env (fun service -> service.Number)
            let taskRequest : Env<ProjectionService, Task<int>> = Env (fun service -> service.TaskNumber)
            let taskResultRequest : Env<ProjectionService, Task<Result<int, string>>> =
                Env (fun service -> service.TaskResultNumber)
            let valueTaskRequest : Env<ProjectionService, ValueTask<int>> =
                Env (fun service -> service.ValueTaskNumber)
            let valueTaskResultRequest : Env<ProjectionService, ValueTask<Result<int, string>>> =
                Env (fun service -> service.ValueTaskResultNumber)
            let coldTaskRequest : Env<ProjectionService, ColdTask<int>> = Env (fun service -> service.ColdTaskNumber)
            let coldTaskResultRequest : Env<ProjectionService, ColdTask<Result<int, string>>> =
                Env (fun service -> service.ColdTaskResultNumber)
            let taskUnitValue : System.Threading.Tasks.Task = Task.CompletedTask
            let valueTaskUnitValue : System.Threading.Tasks.ValueTask = ValueTask()
            let taskUnitRequest : Env<ProjectionService, System.Threading.Tasks.Task> = Env (fun _ -> taskUnitValue)
            let valueTaskUnitRequest : Env<ProjectionService, System.Threading.Tasks.ValueTask> =
                Env (fun _ -> valueTaskUnitValue)
            let coldTaskUnitRequest : Env<ProjectionService, ColdTask<unit>> = Env (fun service -> service.ColdTaskUnit)

            let asyncWorkflow : Flow<ProjectionCaps, string, int> =
                flow {
                    let! (taskValue : int) = taskRequest
                    let! (taskResultValue : int) = taskResultRequest
                    return
                        taskValue
                        + taskResultValue
                }

            let taskWorkflow : Flow<ProjectionCaps, string, int> =
                flow {
                    let! (plain : int) = plainRequest
                    do! taskUnitRequest
                    let! (taskValue : int) = taskRequest
                    let! (taskResultValue : int) = taskResultRequest
                    return
                        plain
                        + taskValue
                        + taskResultValue
                }

            let asyncResult = Flow.run environment asyncWorkflow
            let taskResult = Flow.run environment taskWorkflow

            test <@ asyncResult = Exit.Success 42 @>
            test <@ taskResult = Exit.Success 63 @>

        [<Fact>]
        let ``Flow is sync result only`` () =
            let workflow : Flow<int, string, int> =
                Flow.env
                |> Flow.bind (fun value -> Flow.succeed(value * 2))

            test <@ Flow.run 21 workflow = Exit.Success 42 @>

        [<Fact>]
        let ``Flow runFull and runWithToken mirror run for the default token`` () =
            let workflow : Flow<int, string, int> =
                Flow.env
                |> Flow.map (fun value -> value * 2)

            test <@ Flow.run 21 workflow = Exit.Success 42 @>
            test <@ Flow.runFull 21 CancellationToken.None workflow = Exit.Success 42 @>
            test <@ Flow.runWithToken 21 CancellationToken.None workflow = Exit.Success 42 @>

        [<Fact>]
        let ``Flow delay reruns from scratch`` () =
            let runs = ref 0

            let workflow : Flow<unit, string, int> =
                Flow.delay(fun () ->
                    runs.Value <- runs.Value + 1
                    Flow.succeed runs.Value)

            test <@ Flow.run () workflow = Exit.Success 1 @>
            test <@ Flow.run () workflow = Exit.Success 2 @>

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

            test <@ result = Exit.Success 42 @>

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

            test <@ result = Exit.Success 7 @>

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

            test <@ syncResult = Exit.Success 19 @>
            test <@ asyncResult = Exit.Success 19 @>

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
            test <@ syncMapped = Exit.Success 42 @>
            test <@ syncApplied = Exit.Success 42 @>
            test <@ syncMapped3 = Exit.Success 6 @>
            test <@ syncIgnored = Exit.Success () @>
            test <@ syncBound = Exit.Success 42 @>
            test <@ syncRecovered = Exit.Success 7 @>
            test <@ asyncOkResult = asyncAliasResult @>
            test <@ asyncErrorResult = asyncErrorAliasResult @>
            test <@ asyncMapped = Exit.Success 42 @>
            test <@ asyncApplied = Exit.Success 42 @>
            test <@ asyncMapped3 = Exit.Success 6 @>
            test <@ asyncIgnored = Exit.Success () @>
            test <@ asyncBound = Exit.Success 42 @>
            test <@ asyncRecovered = Exit.Success 7 @>
            test <@ (TaskFlow.run () CancellationToken.None taskOk |> fun task -> task.GetAwaiter().GetResult()) = (TaskFlow.run () CancellationToken.None taskAlias |> fun task -> task.GetAwaiter().GetResult()) @>
            test <@ (TaskFlow.run () CancellationToken.None taskError |> fun task -> task.GetAwaiter().GetResult()) = (TaskFlow.run () CancellationToken.None taskErrorAlias |> fun task -> task.GetAwaiter().GetResult()) @>
            test <@ taskMapped = Exit.Success 42 @>
            test <@ taskApplied = Exit.Success 42 @>
            test <@ taskMapped3 = Exit.Success 6 @>
            test <@ taskIgnored = Exit.Success () @>
            test <@ taskBound = Exit.Success 42 @>
            test <@ taskRecovered = Exit.Success 7 @>

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

            test <@ tapPreservesOriginalError = Exit.Failure (Cause.Fail "primary") @>
            test <@ tapSkipsSuccess = Exit.Success 42 @>
            test <@ List.ofSeq tappedErrors = [ "primary" ] @>
            test <@ recovered = Exit.Success 42 @>
            test <@ bypassesFallback = Exit.Success 10 @>
            test <@ zipped = Exit.Success(6, 10) @>
            test <@ mapped = Exit.Success 16 @>

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

            test <@ tapPreservesOriginalError = Exit.Failure (Cause.Fail "primary") @>
            test <@ tapSkipsSuccess = Exit.Success 42 @>
            test <@ List.ofSeq tappedErrors = [ "primary" ] @>
            test <@ recovered = Exit.Success 42 @>
            test <@ bypassesFallback = Exit.Success 10 @>
            test <@ zipped = Exit.Success(6, 10) @>
            test <@ mapped = Exit.Success 16 @>

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

            test <@ result = Exit.Success 42 @>
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

            test <@ result = Exit.Success 42 @>
            test <@ seen.Value = cts.Token @>

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

            test <@ result = Exit.Success 60 @>

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

            test <@ runOnce () = Exit.Success 1 @>
            test <@ runOnce () = Exit.Success 2 @>

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

            test <@ result = Exit.Success 19 @>

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

            test <@ tapPreservesOriginalError = Exit.Failure (Cause.Fail "primary") @>
            test <@ tapSkipsSuccess = Exit.Success 42 @>
            test <@ List.ofSeq tappedErrors = [ "primary" ] @>
            test <@ recovered = Exit.Success 42 @>
            test <@ bypassesFallback = Exit.Success 10 @>
            test <@ zipped = Exit.Success(6, 10) @>
            test <@ mapped = Exit.Success 16 @>

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

            test <@ flowBridge = Exit.Failure (Cause.Fail "flow:env") @>
            test <@ asyncBridge = Exit.Failure (Cause.Fail "async") @>
            test <@ asyncFlowBridgeFromFlow = Exit.Failure (Cause.Fail "async-flow:env") @>
            test <@ asyncFlowBridge = Exit.Failure (Cause.Fail "async-flow:env") @>
            test <@ taskBridge = Exit.Failure (Cause.Fail "task") @>
            test <@ taskAsyncBridge = Exit.Failure (Cause.Fail "task-async") @>
            test <@ taskFlowBridge = Exit.Failure (Cause.Fail "task-flow:env") @>
            test <@ taskAsyncFlowBridge = Exit.Failure (Cause.Fail "task-async-flow:env") @>
            test <@ flowValue = Exit.Success "flow-value" @>
            test <@ asyncValue = Exit.Success "async-value" @>
            test <@ taskValue = Exit.Success "task-value" @>

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

            test <@ timeoutResult = Exit.Failure (Cause.Fail "timed out") @>
            test <@ retryResult = Exit.Success 42 @>
            test <@ retryRuns.Value = 2 @>
            test <@ acquireReleaseResult = Exit.Failure (Cause.Fail "boom") @>
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

            test <@ timeoutResult = Exit.Failure (Cause.Fail "timed out") @>
            test <@ retryResult = Exit.Success 42 @>
            test <@ retryRuns.Value = 2 @>
            test <@ acquireReleaseResult = Exit.Failure (Cause.Fail "boom") @>
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
                flow {
                    let! context = Flow.env
                    let prefix = context.Runtime.RuntimePrefix
                    let value = context.Environment.Value
                    runtime.Seen.Add $"value={value}"
                    return $"{prefix}{value}"
                }
                |> TaskFlow.fromFlow

            let result =
                workflow
                |> TaskFlow.runContext context
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Exit.Success "rt:41" @>
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
                    flow {
                        let! context = Flow.env
                        return $"{context.Runtime.RuntimePrefix}{context.Environment.Value}"
                    }
                    |> TaskFlow.fromFlow)

            let result =
                spec
                |> TaskFlowSpec.run CancellationToken.None
                |> fun task -> task.GetAwaiter().GetResult()

            test <@ result = Exit.Success "spec:7" @>

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

            let appLayer : Flow<RuntimeContext<RuntimeServices, unit>, string, AppDependencies> =
                Flow.succeed app

            let workflow : Flow<AppDependencies, string, string> =
                flow {
                    let! client = Flow.read _.DeviceClient
                    let! value = Flow.read _.Value
                    return $"{client.Name}:{value}"
                }

            let composed =
                workflow
                |> Flow.provideLayer appLayer

            let composedResult =
                composed
                |> Flow.run outerContext

            let provider = RecordingServiceProvider(typeof<IDeviceClient>, app.DeviceClient :> obj) :> IServiceProvider

            let providerResult =
                Capability.serviceFromProvider<IDeviceClient>
                |> Flow.run provider

            let missingProviderResult =
                Capability.serviceFromProvider<IDeviceClient>
                |> Flow.run (RecordingServiceProvider(typeof<string>, "nope") :> IServiceProvider)

            let flowCapability : Flow<AppDependencies, string, IDeviceClient> =
                Capability.service _.DeviceClient

            let flowCapabilityResult =
                flowCapability
                |> Flow.run app

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

            test <@ composedResult = Exit.Success "provider-client:10" @>
            test <@ providerResult = Exit.Success app.DeviceClient @>
            test <@ missingProviderResult = Exit.Failure (Cause.Fail { CapabilityType = typeof<IDeviceClient> }) @>
            test <@ flowCapabilityResult = Exit.Success app.DeviceClient @>
            test <@ flowLayerResult = Exit.Success "provider-client:10" @>

        [<Fact>]
        let ``Flow traverse and sequence work as expected`` () =
            let values = [ 1; 2; 3 ]
            let workflow = values |> Flow.traverse (fun v -> Flow.succeed (v * 2))
            let result = Flow.run () workflow
            test <@ result = Exit.Success [ 2; 4; 6 ] @>

            let flows = [ Flow.succeed 1; Flow.succeed 2 ]
            let sequenceResult = Flow.run () (Flow.sequence flows)
            test <@ sequenceResult = Exit.Success [ 1; 2 ] @>

            let failWorkflow = [ 1; 2 ] |> Flow.traverse (fun v -> if v = 1 then Flow.fail "error" else Flow.succeed v)
            test <@ Flow.run () failWorkflow = Exit.Failure (Cause.Fail "error") @>

        [<Fact>]
        let ``AsyncFlow traverse and sequence work as expected`` () =
            let values = [ 1; 2; 3 ]
            let workflow = values |> AsyncFlow.traverse (fun v -> AsyncFlow.succeed (v * 2))
            let result = AsyncFlow.run () workflow |> Async.RunSynchronously
            test <@ result = Exit.Success [ 2; 4; 6 ] @>

            let flows = [ AsyncFlow.succeed 1; AsyncFlow.succeed 2 ]
            let sequenceResult = AsyncFlow.run () (AsyncFlow.sequence flows) |> Async.RunSynchronously
            test <@ sequenceResult = Exit.Success [ 1; 2 ] @>

        [<Fact>]
        let ``TaskFlow traverse and sequence work as expected`` () =
            let values = [ 1; 2; 3 ]
            let workflow = values |> TaskFlow.traverse (fun v -> TaskFlow.succeed (v * 2))
            let result = TaskFlow.run () CancellationToken.None workflow |> fun t -> t.GetAwaiter().GetResult()
            test <@ result = Exit.Success [ 2; 4; 6 ] @>

            let flows = [ TaskFlow.succeed 1; TaskFlow.succeed 2 ]
            let sequenceResult = TaskFlow.run () CancellationToken.None (TaskFlow.sequence flows) |> fun t -> t.GetAwaiter().GetResult()
            test <@ sequenceResult = Exit.Success [ 1; 2 ] @>

        [<Fact>]
        let ``AsyncFlow timeout helpers work as expected`` () =
            let okResult = 
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> AsyncFlow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
                |> AsyncFlow.run ()
                |> Async.RunSynchronously
            test <@ okResult = Exit.Success () @>

            let errorResult =
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> AsyncFlow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
                |> AsyncFlow.run ()
                |> Async.RunSynchronously
            test <@ errorResult = Exit.Failure (Cause.Fail "timed out") @>

            let withResult =
                AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> AsyncFlow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> AsyncFlow.succeed ())
                |> AsyncFlow.run ()
                |> Async.RunSynchronously
            test <@ withResult = Exit.Success () @>

        [<Fact>]
        let ``TaskFlow timeout helpers work as expected`` () =
            let okResult = 
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> TaskFlow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
                |> TaskFlow.run () CancellationToken.None
                |> fun t -> t.GetAwaiter().GetResult()
            test <@ okResult = Exit.Success () @>

            let errorResult =
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> TaskFlow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
                |> TaskFlow.run () CancellationToken.None
                |> fun t -> t.GetAwaiter().GetResult()
            test <@ errorResult = Exit.Failure (Cause.Fail "timed out") @>

            let withResult =
                TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
                |> TaskFlow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> TaskFlow.succeed ())
                |> TaskFlow.run () CancellationToken.None
                |> fun t -> t.GetAwaiter().GetResult()
            test <@ withResult = Exit.Success () @>

        [<Fact>]
        let ``flow computation expression mixes sync, async, task, result, and env requests`` () =
            let service = ProjectionService()
            let request : Env<ProjectionService> = Unchecked.defaultof<_>
            let asyncNumberRequest : Env<ProjectionService, Async<int>> = Env(fun value -> value.AsyncNumber)
            let asyncResultNumberRequest : Env<ProjectionService, Async<Result<int, string>>> =
                Env(fun value -> value.AsyncResultNumber)
            let taskNumberRequest : Env<ProjectionService, Task<int>> = Env(fun value -> value.TaskNumber)
            let taskResultNumberRequest : Env<ProjectionService, Task<Result<int, string>>> =
                Env(fun value -> value.TaskResultNumber)
            let resultNumberRequest : Env<ProjectionService, Result<int, string>> =
                Env(fun value -> value.NumberResult)

            let workflow : Flow<ProjectionCaps, string, int> =
                flow {
                    let! (env : ProjectionService) = request
                    do! Task.CompletedTask
                    let! asyncValue = (async { return env.Number } : Async<int>)
                    let! taskValue = (Task.FromResult env.Number : Task<int>)
                    let! (resultValue : int) = (Ok env.Number : Result<int, string>)
                    let! requestAsyncValue = asyncNumberRequest
                    let! requestAsyncResultValue = asyncResultNumberRequest
                    let! requestTaskValue = taskNumberRequest
                    let! requestTaskResultValue = taskResultNumberRequest
                    let! requestResultValue = resultNumberRequest
                    return
                        asyncValue
                        + taskValue
                        + resultValue
                        + requestAsyncValue
                        + requestAsyncResultValue
                        + requestTaskValue
                        + requestTaskResultValue
                        + requestResultValue
                }

            let publicMethods = publicInstanceMethodNames typeof<FlowBuilder>
            let argumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

            test <@ Flow.run (ProjectionCaps()) workflow = Exit.Success (service.Number * 8) @>
            test <@ publicMethods |> Array.contains "Bind" @>
            test <@ publicMethods |> Array.contains "Yield" @>
            test <@ publicMethods |> Array.contains "YieldFrom" @>
            test <@ publicMethods |> Array.contains "ReturnFrom" @>
            test <@ argumentTypeNames = [| "Env`1"; "Env`2"; "FSharpAsync`1"; "FSharpFunc`2"; "FSharpOption`1"; "FSharpResult`2"; "FSharpValueOption`1"; "Flow`3"; "Task"; "Task`1" |] @>

        [<Fact>]
        let ``flow builders directly bind Result and Result unit values`` () =
            let syncWorkflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! doubled = Ok(env * 2)
                    do! Ok ()
                    return doubled
                }

            let asyncWorkflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! doubled = Ok(env * 2)
                    do! Ok ()
                    return doubled
                }

            let taskWorkflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! doubled = Ok(env * 2)
                    do! Ok ()
                    return doubled
                }

            test <@ Flow.run 21 syncWorkflow = Exit.Success 42 @>
            test <@ Flow.run 21 asyncWorkflow = Exit.Success 42 @>
            test <@ Flow.run 21 taskWorkflow = Exit.Success 42 @>

        [<Fact>]
        let ``flow computation expression mixes sync async task and result effects in one workflow`` () =
            let workflow : Flow<string, string, string> =
                flow {
                    let! prefix = Flow.read id
                    let! asyncSuffix = async { return "-async" }
                    do! Task.CompletedTask
                    let! taskSuffix = Task.FromResult "-task"
                    let! resultSuffix = Ok "-result"
                    return prefix + asyncSuffix + taskSuffix + resultSuffix
                }

            test <@ Flow.run "flow" workflow = Exit.Success "flow-async-task-result" @>

        [<Fact>]
        let ``flow builder overloads stay aligned with the Fable 5 mapping`` () =
            let publicMethods = publicInstanceMethodNames typeof<FlowBuilder>
            let argumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

            test <@ publicMethods |> Array.contains "Bind" @>
            test <@ publicMethods |> Array.contains "ReturnFrom" @>
            test <@ publicMethods |> Array.contains "YieldFrom" @>
            test <@ publicMethods |> Array.contains "Yield" @>
            test <@ publicMethods |> Array.contains "Run" @>
            test <@ argumentTypeNames = [| "Env`1"; "Env`2"; "FSharpAsync`1"; "FSharpFunc`2"; "FSharpOption`1"; "FSharpResult`2"; "FSharpValueOption`1"; "Flow`3"; "Task"; "Task`1" |] @>

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

            let asyncProjection : Flow<ReaderEnv, string, string> =
                flow {
                    yield _.Prefix
                }

            let asyncYieldFrom : Flow<ReaderEnv, string, string> =
                flow {
                    yield! Flow.read _.Prefix
                }

            let taskProjection : Flow<ReaderEnv, string, string> =
                flow {
                    yield _.Prefix
                }

            let taskYieldFrom : Flow<ReaderEnv, string, string> =
                flow {
                    yield! Flow.read _.Prefix
                }

            test <@ Flow.run environment syncValue = Exit.Success 42 @>
            test <@ Flow.run environment syncProjection = Exit.Success "flow" @>
            test <@ Flow.run environment syncYieldFrom = Exit.Success "flow" @>
            test <@ Flow.run environment asyncProjection = Exit.Success "flow" @>
            test <@ Flow.run environment asyncYieldFrom = Exit.Success "flow" @>
            test <@ Flow.run environment taskProjection = Exit.Success "flow" @>
            test <@ Flow.run environment taskYieldFrom = Exit.Success "flow" @>

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

            let asyncWorkflow : Flow<int, unit, int> =
                flow {
                    let! env = Flow.env
                    let! value = Some(env + 1)
                    let! extra = ValueSome(value + 1)
                    return extra * 2
                }

            let asyncReturnFromNone : Flow<unit, unit, int> =
                flow { return! None }

            let taskWorkflow : Flow<int, unit, int> =
                flow {
                    let! env = Flow.env
                    let! value = Some(env + 1)
                    let! extra = ValueSome(value + 1)
                    return extra * 2
                }

            let taskReturnFromValueNone : Flow<unit, unit, int> =
                flow { return! ValueNone }

            let flowArgumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

            test <@ Flow.run 20 syncSome = Exit.Success 42 @>
            test <@ Flow.run 20 syncNone = Exit.Failure (Cause.Fail ()) @>
            test <@ Flow.run 20 syncValueSome = Exit.Success 42 @>
            test <@ Flow.run 20 syncValueNone = Exit.Failure (Cause.Fail ()) @>
            test <@ Flow.run 19 asyncWorkflow = Exit.Success 42 @>
            test <@ Flow.run () asyncReturnFromNone = Exit.Failure (Cause.Fail ()) @>
            test <@ Flow.run 19 taskWorkflow = Exit.Success 42 @>
            test <@ Flow.run () taskReturnFromValueNone = Exit.Failure (Cause.Fail ()) @>
            test <@ flowArgumentTypeNames |> Array.contains "FSharpOption`1" @>
            test <@ flowArgumentTypeNames |> Array.contains "FSharpResult`2" @>
            test <@ flowArgumentTypeNames |> Array.contains "FSharpValueOption`1" @>

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

    let probe : Flow<unit, string, int> =
        flow {{
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

    let probe : Flow<unit, string, int> =
        flow {{
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
            test <@ asyncOutput.Contains("Flow<unit,unit,int>") @>
            test <@ taskExitCode <> 0 @>
            test <@ taskOutput.Contains("Flow<unit,unit,int>") @>

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

            test <@ syncSome = Exit.Success 42 @>
            test <@ syncNone = Exit.Failure (Cause.Fail "missing value") @>
            test <@ syncValueSome = Exit.Success 42 @>
            test <@ syncValueNone = Exit.Failure (Cause.Fail "missing value") @>
            test <@ asyncSome = Exit.Success 42 @>
            test <@ asyncNone = Exit.Failure (Cause.Fail "missing value") @>
            test <@ asyncValueSome = Exit.Success 42 @>
            test <@ asyncValueNone = Exit.Failure (Cause.Fail "missing value") @>
            test <@ taskSome = Exit.Success 42 @>
            test <@ taskNone = Exit.Failure (Cause.Fail "missing value") @>
            test <@ taskValueSome = Exit.Success 42 @>
            test <@ taskValueNone = Exit.Failure (Cause.Fail "missing value") @>

        [<Fact>]
        let ``flow lives in FsFlow and composes sync flows`` () =
            let workflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! baseValue = Flow.succeed(env + 1)
                    return baseValue * 2
                }

            let result =
                workflow
                |> Flow.run 20

            test <@ typeof<FlowBuilder>.Namespace = "FsFlow" @>
            test <@ result = Exit.Success 42 @>

        [<Fact>]
        let ``flow directly binds and returns Async and Async Result values`` () =
            let workflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! baseValue = async { return env + 1 }
                    let! (adjustedValue : int) = async { return Ok(baseValue * 2) }
                    return adjustedValue + 2
                }

            let asyncReturnFrom : Flow<unit, string, int> =
                flow { return! async { return 42 } }

            let workflowResult =
                workflow
                |> Flow.run 19

            let asyncReturnFromResult =
                asyncReturnFrom
                |> Flow.run ()

            test <@ workflowResult = Exit.Success 42 @>
            test <@ asyncReturnFromResult = Exit.Success 42 @>
            test <@ hasAsyncResultReturnFromOverload typeof<FlowBuilder> @>

        [<Fact>]
        let ``flow lives in FsFlow and composes async flows`` () =
            let workflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! baseValue = Flow.succeed(env + 1)
                    return baseValue * 2
                }

            let result =
                workflow
                |> Flow.run 20

            test <@ typeof<FlowBuilder>.Namespace = "FsFlow" @>
            test <@ result = Exit.Success 42 @>

        [<Fact>]
        let ``flow directly binds and returns Async, Task, and result-bearing values`` () =
            let resultTask (value: int) : Task<Result<int, string>> = Task.FromResult(Ok value)

            let workflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    do! Task.CompletedTask
                    let! baseValue = async { return env + 1 }
                    let! adjustedValue = async { return Ok(baseValue * 2) }
                    return adjustedValue + 2
                }

            let asyncReturnFrom : Flow<unit, string, int> =
                flow { return! async { return 42 } }

            let taskWorkflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    do! Task.CompletedTask
                    let! baseValue = Task.FromResult(env + 1)
                    let! (adjustedValue : int) = resultTask (baseValue * 2)
                    return adjustedValue + 2
                }

            let taskReturnFrom : Flow<unit, string, unit> =
                flow { return! Task.CompletedTask }

            let taskReturnFromResult : Flow<unit, string, int> =
                flow {
                    let! (value : int) = resultTask 42
                    return value
                }

            let run flow environment =
                Flow.run environment flow

            let workflowResult = run workflow 19
            let asyncReturnFromResult = run asyncReturnFrom ()
            let taskWorkflowResult = run taskWorkflow 19
            let taskReturnFromUnitResult = run taskReturnFrom ()
            let taskReturnFromResultResult = run taskReturnFromResult ()

            test <@ workflowResult = Exit.Success 42 @>
            test <@ asyncReturnFromResult = Exit.Success 42 @>
            test <@ taskWorkflowResult = Exit.Success 42 @>
            test <@ taskReturnFromUnitResult = Exit.Success() @>
            test <@ taskReturnFromResultResult = Exit.Success 42 @>
            test <@ hasAsyncResultReturnFromOverload typeof<FlowBuilder> @>

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

            test <@ workflowResult = Exit.Success 42 @>
            test <@ valueTaskReturnFromUnitResult = Exit.Success() @>
            test <@ valueTaskReturnFromValueResult = Exit.Success 42 @>
            test <@ valueTaskReturnFromResultResult = Exit.Success 42 @>

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

            test <@ runningTask.GetType() = typeof<Task<Exit<int, string>>> @>
            test <@ result = Exit.Success 42 @>

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

            test <@ workflowResult = Exit.Success 42 @>
            test <@ coldTaskReturnFromValueResult = Exit.Success 42 @>
            test <@ coldTaskReturnFromResultResult = Exit.Success 42 @>
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

            test <@ workflowResult = Exit.Success 42 @>
            test <@ taskReturnFromUnitResult = Exit.Success() @>
            test <@ taskReturnFromResultResult = Exit.Success 42 @>

        [<Fact>]
        let ``taskFlow directly binds and returns ValueTask values when task helpers are imported`` () =
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
                taskFlow {
                    let! (value : int) = resultValueTask 42
                    return value
                }

            let workflowResult = TaskFlow.run 19 CancellationToken.None workflow |> fun task -> task.GetAwaiter().GetResult()
            let valueTaskReturnFromUnitResult = TaskFlow.run () CancellationToken.None valueTaskReturnFrom |> fun task -> task.GetAwaiter().GetResult()
            let valueTaskReturnFromValueResult = TaskFlow.run () CancellationToken.None valueTaskReturnFromValue |> fun task -> task.GetAwaiter().GetResult()
            let valueTaskReturnFromResultResult = TaskFlow.run () CancellationToken.None valueTaskReturnFromResult |> fun task -> task.GetAwaiter().GetResult()

            test <@ workflowResult = Exit.Success 42 @>
            test <@ valueTaskReturnFromUnitResult = Exit.Success() @>
            test <@ valueTaskReturnFromValueResult = Exit.Success 42 @>
            test <@ valueTaskReturnFromResultResult = Exit.Success 42 @>

        [<Fact>]
        let ``asyncFlow directly binds async values when task helpers are imported`` () =
            let workflow : Flow<int, string, int> =
                flow {
                    let! env = Flow.env
                    let! baseValue = async { return env + 1 }
                    let! adjustedValue = async { return baseValue * 2 }
                    return adjustedValue + 2
                }

            let workflowResult = Flow.run 19 workflow

            test <@ workflowResult = Exit.Success 42 @>

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
                flow {
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
            let asyncFlowResult = Flow.run () asyncFlowTest
            let taskFlowResult = TaskFlow.run () CancellationToken.None taskFlowTest |> fun t -> t.GetAwaiter().GetResult()

            test <@ flowResult = Exit.Success 52 @>
            test <@ asyncFlowResult = Exit.Success 52 @>
            test <@ taskFlowResult = Exit.Success 60 @>

        [<Fact>]
        let ``AsyncFlow login syntax uses Guard constructors and error mapping`` () =
            let tryGetUser username = async { return if username = "missing" then None else Some username }
            let isPwdValid password user = password = $"{user}-pwd"
            let authorize user = async { return if user = "blocked" then Error "denied" else Ok () }
            let createAuthToken user = if user = "expired" then Error "token-expired" else Ok $"token-{user}"

            let login username password =
                flow {
                    let userResult : Async<Result<string, LoginError>> = Guard.Of(InvalidUser, tryGetUser username)
                    let! (user : string) = userResult

                    let passwordCheck : Result<unit, LoginError> = Guard.Of(InvalidPwd, isPwdValid password user)
                    do! passwordCheck

                    let authorizeResult : Async<Result<unit, LoginError>> = Guard.MapError(Unauthorized, authorize user)
                    do! authorizeResult

                    let tokenResult : Result<string, LoginError> = Guard.MapError(TokenErr, createAuthToken user)
                    return! tokenResult
                }

            let success = Flow.run () (login "alice" "alice-pwd")
            let authFailure = Flow.run () (login "blocked" "blocked-pwd")
            let tokenFailure = Flow.run () (login "expired" "expired-pwd")

            test <@ success = Exit.Success "token-alice" @>
            test <@ authFailure = Exit.Failure (Cause.Fail (Unauthorized "denied")) @>
            test <@ tokenFailure = Exit.Failure (Cause.Fail (TokenErr "token-expired")) @>

        [<Fact>]
        let ``Guard mapError stays symmetric across flow families`` () =
            let asyncSource : Async<Result<int, string>> = async { return Error "async-source" }
            let taskSource : Task<Result<int, string>> = task { return Error "task-source" }
            let asyncSuccess : Async<Result<int, string>> = async { return Ok 1 }

            let asyncMapped =
                let mappedAsyncSource : Async<Result<int, string>> =
                    Guard.MapError((fun error -> $"mapped-{error}"), asyncSource)

                flow {
                    let! value = mappedAsyncSource
                    return value + 1
                }

            let taskMapped =
                let mappedAsyncSuccess : Async<Result<int, string>> =
                    Guard.MapError((fun error -> $"mapped-{error}"), asyncSuccess)

                let mappedTaskSource : Task<Result<int, string>> =
                    Guard.MapError((fun error -> $"mapped-{error}"), taskSource)

                flow {
                    let! (asyncValue : int) = mappedAsyncSuccess
                    let! (taskValue : int) = mappedTaskSource
                    return asyncValue + taskValue
                }

            test <@ Flow.run () asyncMapped = Exit.Failure (Cause.Fail "mapped-async-source") @>
            test <@ Flow.run () taskMapped = Exit.Failure (Cause.Fail "mapped-task-source") @>

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
            let asyncFlowFail = flow {
                let! (value : int) = guardedAsyncFlowFail
                return value
            }
            let taskFlowFail = flow {
                let! (value : int) = guardedTaskFlowFail
                return value
            }

            test <@ Flow.run () flowFail = Exit.Failure (Cause.Fail "failed") @>
            test <@ Flow.run () asyncFlowFail = Exit.Failure (Cause.Fail "failed") @>
            test <@ Flow.run () taskFlowFail = Exit.Failure (Cause.Fail "failed") @>
