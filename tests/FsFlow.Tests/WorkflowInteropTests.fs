namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowInteropTests =
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

        let flowRun1 = Flow.runSync flowCaps flowWorkflow
        let flowRun2 = Flow.runSync flowCaps flowWorkflow
        let asyncRun1 = Flow.runSync asyncCaps asyncWorkflow
        let asyncRun2 = Flow.runSync asyncCaps asyncWorkflow
        let taskRun1 = Flow.runSync taskCaps taskWorkflow
        let taskRun2 = Flow.runSync taskCaps taskWorkflow

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
    let ``projected Env requests bind through flow core shapes`` () =
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

        test <@ Flow.runSync environment flowWorkflow = Exit.Success 84 @>

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

        test <@ Flow.runSync environment asyncWorkflow = Exit.Success 21 @>

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

        let asyncResult = Flow.runSync environment asyncWorkflow
        let taskResult = Flow.runSync environment taskWorkflow

        test <@ asyncResult = Exit.Success 42 @>
        test <@ taskResult = Exit.Success 63 @>

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

        test <@ Flow.runSync (ProjectionCaps()) workflow = Exit.Success (service.Number * 8) @>

    [<Fact>]
    let ``flow builders directly bind Result and Result unit values`` () =
        let syncWorkflow : Flow<int, string, int> =
            flow {
                let! env = Flow.env
                let! doubled = Ok(env * 2)
                do! Ok ()
                return doubled
            }

        test <@ Flow.runSync 21 syncWorkflow = Exit.Success 42 @>

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

        test <@ Flow.runSync "flow" workflow = Exit.Success "flow-async-task-result" @>

    [<Fact>]
    let ``reader-style projection uses Flow.read and return! passthrough`` () =
        let environment : ReaderEnv =
            { Prefix = "flow"
              Count = 21 }

        let syncValue : Flow<ReaderEnv, string, int> =
            flow {
                return 42
            }

        let syncProjection : Flow<ReaderEnv, string, string> =
            Flow.read _.Prefix

        let syncReturnFrom : Flow<ReaderEnv, string, string> =
            flow {
                return! Flow.read _.Prefix
            }

        test <@ Flow.runSync environment syncValue = Exit.Success 42 @>
        test <@ Flow.runSync environment syncProjection = Exit.Success "flow" @>
        test <@ Flow.runSync environment syncReturnFrom = Exit.Success "flow" @>

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
            |> Flow.runSync 19

        let asyncReturnFromResult =
            asyncReturnFrom
            |> Flow.runSync ()

        test <@ workflowResult = Exit.Success 42 @>
        test <@ asyncReturnFromResult = Exit.Success 42 @>
        test <@ hasAsyncResultReturnFromOverload typeof<FlowBuilder> @>

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
            Flow.runSync environment flow

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
    let ``asyncFlow directly binds async values when task helpers are imported`` () =
        let workflow : Flow<int, string, int> =
            flow {
                let! env = Flow.env
                let! baseValue = async { return env + 1 }
                let! adjustedValue = async { return baseValue * 2 }
                return adjustedValue + 2
            }

        let workflowResult = Flow.runSync 19 workflow

        test <@ workflowResult = Exit.Success 42 @>
