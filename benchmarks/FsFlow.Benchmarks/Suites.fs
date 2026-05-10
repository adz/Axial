namespace FsFlow.Benchmarks

open System.Threading
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open FsFlow

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type ReaderOverheadBenchmarks() =
    let taskFlowLocalEnv = Shared.buildTaskFlowLocalEnvChain ()
    let manualLocalEnv = Shared.buildManualTaskLocalEnvChain ()
    let asyncLocalReader = Shared.buildAsyncLocalReaderChain ()

    [<Benchmark(Baseline = true, Description = "TaskFlow localEnv x10")>]
    member _.TaskFlowLocalEnvX10() =
        taskFlowLocalEnv
        |> TaskFlow.run 0 Shared.noCancellation
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> Shared.consumeExit

    [<Benchmark(Description = "Manual env passing x10")>]
    member _.ManualEnvPassingX10() =
        manualLocalEnv 0 Shared.noCancellation
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> Shared.consumeResult

    [<Benchmark(Description = "AsyncLocal updates x10")>]
    member _.AsyncLocalUpdatesX10() =
        asyncLocalReader 0 Shared.noCancellation
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> Shared.consumeResult

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type AsyncRailwayBenchmarks() =
    let mutable failAt = None
    let mutable asyncFlow = AsyncFlow.succeed 0
    let mutable fsToolkitAsync = Shared.buildFsToolkitAsyncResultBindChain Shared.RailwayDepth None
    let mutable directAsync = Shared.buildDirectAsyncResultBindChain Shared.RailwayDepth None

    [<Params(1, 20)>]
    member val FailAt = 1 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        failAt <- Some this.FailAt
        asyncFlow <- Shared.buildAsyncFlowBindChain Shared.RailwayDepth failAt
        fsToolkitAsync <- Shared.buildFsToolkitAsyncResultBindChain Shared.RailwayDepth failAt
        directAsync <- Shared.buildDirectAsyncResultBindChain Shared.RailwayDepth failAt

    [<Benchmark(Baseline = true)>]
    member _.AsyncFlow() =
        Shared.runAsyncFlow asyncFlow

    [<Benchmark(Description = "FsToolkit asyncResult")>]
    member _.FsToolkitAsyncResult() =
        Shared.runAsyncResult fsToolkitAsync

    [<Benchmark(Description = "Direct Async<Result>")>]
    member _.DirectAsyncResult() =
        Shared.runAsyncResult directAsync

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type TaskRailwayBenchmarks() =
    let mutable failAt = None
    let mutable taskFlow = TaskFlow.succeed 0
    let mutable fsToolkitTask = Shared.buildFsToolkitTaskResultBindChain Shared.RailwayDepth None
    let mutable directTask = Shared.buildDirectTaskResultBindChain Shared.RailwayDepth None

    [<Params(1, 20)>]
    member val FailAt = 1 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        failAt <- Some this.FailAt
        taskFlow <- Shared.buildTaskFlowBindChain Shared.RailwayDepth failAt
        fsToolkitTask <- Shared.buildFsToolkitTaskResultBindChain Shared.RailwayDepth failAt
        directTask <- Shared.buildDirectTaskResultBindChain Shared.RailwayDepth failAt

    [<Benchmark(Baseline = true)>]
    member _.TaskFlow() =
        Shared.runTaskFlow taskFlow

    [<Benchmark(Description = "FsToolkit taskResult")>]
    member _.FsToolkitTaskResult() =
        Shared.runTaskResult fsToolkitTask

    [<Benchmark(Description = "Direct Task<Result>")>]
    member _.DirectTaskResult() =
        Shared.runTaskResult directTask

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type CompositionChainBenchmarks() =
    let flowMap = Shared.buildFlowMapChain Shared.CompositionDepth
    let flowBind = Shared.buildFlowBindChain Shared.CompositionDepth
    let asyncFlowBind = Shared.buildAsyncFlowBindChain Shared.CompositionDepth None
    let taskFlowBind = Shared.buildTaskFlowBindChain Shared.CompositionDepth None
    let directAsyncBind = Shared.buildDirectAsyncResultBindChain Shared.CompositionDepth None
    let directTaskBind = Shared.buildDirectTaskResultBindChain Shared.CompositionDepth None
    let rawTaskBind = Shared.buildRawTaskBindChain Shared.CompositionDepth

    [<Benchmark(Description = "Flow map x100")>]
    member _.FlowMapX100() =
        Shared.runFlow flowMap

    [<Benchmark(Description = "Flow bind x100")>]
    member _.FlowBindX100() =
        Shared.runFlow flowBind

    [<Benchmark(Description = "AsyncFlow bind x100")>]
    member _.AsyncFlowBindX100() =
        Shared.runAsyncFlow asyncFlowBind

    [<Benchmark(Baseline = true, Description = "TaskFlow bind x100")>]
    member _.TaskFlowBindX100() =
        Shared.runTaskFlow taskFlowBind

    [<Benchmark(Description = "Direct Async<Result> bind x100")>]
    member _.DirectAsyncResultBindX100() =
        Shared.runAsyncResult directAsyncBind

    [<Benchmark(Description = "Direct Task<Result> bind x100")>]
    member _.DirectTaskResultBindX100() =
        Shared.runTaskResult directTaskBind

    [<Benchmark(Description = "Raw Task bind x100")>]
    member _.RawTaskBindX100() =
        Shared.runTask rawTaskBind

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type CancellationFlowBenchmarks() =
    let taskFlow = Shared.buildTaskFlowCancellationChain ()
    let directTask = Shared.buildDirectCancellableTaskChain ()

    [<Benchmark(Baseline = true)>]
    member _.TaskFlow() =
        Shared.runTaskFlow taskFlow

    [<Benchmark(Description = "Explicit token Task<Result>")>]
    member _.ExplicitTokenTaskResult() =
        Shared.runCancellableTaskResult directTask

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type CancellableTaskBenchmarks() =
    let icedTasksTask = Shared.buildIcedTasksCancellableTaskChain ()
    let directTask = Shared.buildDirectCancellableTaskValueChain ()

    [<Benchmark(Baseline = true)>]
    member _.IcedTasksCancellableTask() =
        Shared.runCancellableTask icedTasksTask

    [<Benchmark(Description = "Manual token Task")>]
    member _.ManualTokenTask() =
        Shared.runCancellableTask directTask

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type SynchronousCompletionBenchmarks() =
    let taskFlow =
        TaskFlow.succeed 1
        |> TaskFlow.bind (fun value -> TaskFlow.succeed(value + 1))
        |> TaskFlow.bind (fun value -> TaskFlow.succeed(value * 2))
        |> TaskFlow.map (fun value -> value + 3)

    let valueTaskFlow =
        CandidateValueTaskFlow.succeed 1
        |> CandidateValueTaskFlow.bind (fun value -> CandidateValueTaskFlow.succeed(value + 1))
        |> CandidateValueTaskFlow.bind (fun value -> CandidateValueTaskFlow.succeed(value * 2))
        |> CandidateValueTaskFlow.map (fun value -> value + 3)

    let plyValueTask = Shared.buildPlyValueTaskChain 3

    [<Benchmark(Baseline = true)>]
    member _.TaskFlow() =
        taskFlow
        |> TaskFlow.run 0 CancellationToken.None
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> Shared.consumeExit

    [<Benchmark(Description = "Candidate ValueTaskFlow")>]
    member _.CandidateValueTaskFlow() =
        valueTaskFlow
        |> CandidateValueTaskFlow.run 0 CancellationToken.None
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> Shared.consumeValueTaskResult

    [<Benchmark(Description = "Ply vtask")>]
    member _.PlyValueTask() =
        plyValueTask
        |> Shared.runValueTaskResult
