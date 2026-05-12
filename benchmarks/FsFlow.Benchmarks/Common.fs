namespace FsFlow.Benchmarks

open System
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks
open FsToolkit.ErrorHandling
open IcedTasks
open FsFlow

[<RequireQualifiedAccess>]
module internal Shared =
    [<Literal>]
    let ReaderDepth = 10

    [<Literal>]
    let RailwayDepth = 20

    [<Literal>]
    let CompositionDepth = 100

    [<Literal>]
    let CancellationCheckpoints = 5

    let noCancellation = CancellationToken.None

    let consumeResult (result: Result<int, string>) =
        match result with
        | Ok value -> value
        | Error error -> error.Length

    let consumeExit (exit: Exit<int, string>) =
        match exit with
        | Exit.Success value -> value
        | Exit.Failure (Cause.Fail error) -> error.Length
        | Exit.Failure _ -> -1

    let consumeValueTaskResult (result: Result<int, string>) =
        consumeResult result

    let runFlow (flow: Flow<int, string, int>) =
        flow
        |> Flow.run 0
        |> fun t -> t.AsTask().GetAwaiter().GetResult()
        |> consumeExit

    let runAsyncFlow (flow: AsyncFlow<int, string, int>) =
        flow
        |> AsyncFlow.run 0
        |> Async.RunSynchronously
        |> consumeExit

    let runTaskFlow (flow: TaskFlow<int, string, int>) =
        flow
        |> TaskFlow.run 0 noCancellation
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> consumeExit

    let runTaskResult (workflow: unit -> Task<Result<int, string>>) =
        workflow ()
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> consumeResult

    let runAsyncResult (workflow: unit -> Async<Result<int, string>>) =
        workflow ()
        |> Async.RunSynchronously
        |> consumeResult

    let runTask (workflow: unit -> Task<int>) =
        workflow ()
        |> fun operation -> operation.GetAwaiter().GetResult()

    let runValueTask (workflow: unit -> ValueTask<int>) =
        workflow ()
        |> fun operation -> operation.GetAwaiter().GetResult()

    let runValueTaskResult (workflow: unit -> ValueTask<Result<int, string>>) =
        workflow ()
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> consumeResult

    let runCancellableTaskResult (workflow: CancellationToken -> Task<Result<int, string>>) =
        workflow noCancellation
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> consumeResult

    let runCancellableTask (workflow: CancellationToken -> Task<int>) =
        workflow noCancellation
        |> fun operation -> operation.GetAwaiter().GetResult()

    let buildFlowBindChain (depth: int) =
        let mutable flow = Flow.succeed 0

        for index in 1 .. depth do
            flow <-
                flow
                |> Flow.bind (fun value -> Flow.succeed(value + index))

        flow

    let buildFlowMapChain (depth: int) =
        let mutable flow = Flow.succeed 0

        for index in 1 .. depth do
            flow <- flow |> Flow.map (fun value -> value + index)

        flow

    let buildAsyncFlowBindChain (depth: int) (failAt: int option) =
        let mutable flow = AsyncFlow.succeed 0

        for index in 1 .. depth do
            flow <-
                flow
                |> AsyncFlow.bind (fun value ->
                    if failAt = Some index then
                        AsyncFlow.fail $"fail-{index}"
                    else
                        AsyncFlow.succeed(value + index))

        flow

    let buildTaskFlowBindChain (depth: int) (failAt: int option) =
        let mutable flow = TaskFlow.succeed 0

        for index in 1 .. depth do
            flow <-
                flow
                |> TaskFlow.bind (fun value ->
                    if failAt = Some index then
                        TaskFlow.fail $"fail-{index}"
                    else
                        TaskFlow.succeed(value + index))

        flow

    let buildDirectAsyncResultBindChain (depth: int) (failAt: int option) =
        let rec loop index value =
            async {
                if index > depth then
                    return Ok value
                elif failAt = Some index then
                    return Error $"fail-{index}"
                else
                    let! next = async.Return(value + index)
                    return! loop (index + 1) next
            }

        fun () -> loop 1 0

    let buildFsToolkitAsyncResultBindChain (depth: int) (failAt: int option) =
        let rec loop index value =
            asyncResult {
                if index > depth then
                    return value
                elif failAt = Some index then
                    return! Error $"fail-{index}"
                else
                    let! next = async { return value + index }
                    return! loop (index + 1) next
            }

        fun () -> loop 1 0

    let buildFsToolkitTaskResultBindChain (depth: int) (failAt: int option) =
        let rec loop index value =
            taskResult {
                if index > depth then
                    return value
                elif failAt = Some index then
                    return! Error $"fail-{index}"
                else
                    let! next = Task.FromResult(value + index)
                    return! loop (index + 1) next
            }

        fun () -> loop 1 0

    let buildDirectTaskResultBindChain (depth: int) (failAt: int option) =
        let rec loop index value () =
            task {
                if index > depth then
                    return Ok value
                elif failAt = Some index then
                    return Error $"fail-{index}"
                else
                    let! next = Task.FromResult(value + index)
                    return! loop (index + 1) next ()
            }

        loop 1 0

    let buildPlyValueTaskChain (depth: int) =
        let rec loop index value =
            vtask {
                if index > depth then
                    return Ok value
                else
                    let! next = ValueTask<int>(value + index)
                    return! loop (index + 1) next
            }

        fun () -> loop 1 0

    let buildRawTaskBindChain (depth: int) =
        let rec loop index value () =
            task {
                if index > depth then
                    return value
                else
                    let! next = Task.FromResult(value + index)
                    return! loop (index + 1) next ()
            }

        loop 1 0

    let buildTaskFlowLocalEnvChain () =
        let baseFlow =
            TaskFlow.read id
            |> TaskFlow.map (fun value -> value * 2)

        let mutable flow = baseFlow

        for _ in 1 .. ReaderDepth do
            flow <- flow |> TaskFlow.localEnv ((+) 1)

        flow

    let buildManualTaskLocalEnvChain () =
        let baseWorkflow =
            fun (environment: int) (_: CancellationToken) ->
                Task.FromResult(Ok(environment * 2))

        let mutable workflow = baseWorkflow

        for _ in 1 .. ReaderDepth do
            let previous = workflow
            workflow <- fun environment cancellationToken -> previous (environment + 1) cancellationToken

        workflow

    let buildAsyncLocalReaderChain () =
        let current = AsyncLocal<int>()

        fun (_: int) (_: CancellationToken) ->
            let mutable previousValues = Array.zeroCreate<int> ReaderDepth

            for index in 0 .. ReaderDepth - 1 do
                previousValues[index] <- current.Value
                current.Value <- current.Value + 1

            let result = current.Value * 2

            for index in ReaderDepth - 1 .. -1 .. 0 do
                current.Value <- previousValues[index]

            Task.FromResult(Ok result)

    let buildTaskFlowCancellationChain () =
        let checkpoint index =
            TaskFlow.fromTaskResult(
                ColdTask(fun cancellationToken ->
                    cancellationToken.ThrowIfCancellationRequested()
                    Task.FromResult(Ok index))
            )

        let mutable flow = TaskFlow.succeed 0

        for index in 1 .. CancellationCheckpoints do
            flow <-
                flow
                |> TaskFlow.bind (fun value ->
                    checkpoint index
                    |> TaskFlow.map (fun checkpointValue -> value + checkpointValue))

        flow

    let buildDirectCancellableTaskChain () =
        let rec loop index value (cancellationToken: CancellationToken) =
            task {
                if index > CancellationCheckpoints then
                    return Ok value
                else
                    cancellationToken.ThrowIfCancellationRequested()
                    let! checkpointValue = Task.FromResult index
                    return! loop (index + 1) (value + checkpointValue) cancellationToken
            }

        fun cancellationToken -> loop 1 0 cancellationToken

    let buildIcedTasksCancellableTaskChain () =
        let checkpoint index =
            cancellableTask {
                let! ct = CancellableTask.getCancellationToken ()
                ct.ThrowIfCancellationRequested()
                return index
            }

        let mutable workflow = cancellableTask { return 0 }

        for index in 1 .. CancellationCheckpoints do
            workflow <-
                cancellableTask {
                    let! value = workflow
                    let! checkpointValue = checkpoint index
                    return value + checkpointValue
                }

        workflow

    let buildDirectCancellableTaskValueChain () =
        let rec loop index value (cancellationToken: CancellationToken) =
            task {
                if index > CancellationCheckpoints then
                    return value
                else
                    cancellationToken.ThrowIfCancellationRequested()
                    let! checkpointValue = Task.FromResult index
                    return! loop (index + 1) (value + checkpointValue) cancellationToken
            }

        fun cancellationToken -> loop 1 0 cancellationToken
