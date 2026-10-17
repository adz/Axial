namespace Axial.Benchmarks

open System.Threading
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Axial

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type ReaderOverheadBenchmarks() =
    let flowLocalEnv = Shared.buildFlowLocalEnvChain ()
    let manualLocalEnv = Shared.buildManualTaskLocalEnvChain ()
    let asyncLocalReader = Shared.buildAsyncLocalReaderChain ()

    [<Benchmark(Baseline = true, Description = "Flow localEnv x10")>]
    member _.FlowLocalEnvX10() =
        flowLocalEnv.RunSynchronously(0)
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
    let mutable flow = Flow.succeed 0
    let mutable fsToolkitAsync = Shared.buildFsToolkitAsyncResultBindChain Shared.RailwayDepth None
    let mutable directAsync = Shared.buildDirectAsyncResultBindChain Shared.RailwayDepth None

    [<Params(1, 20)>]
    member val FailAt = 1 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        failAt <- Some this.FailAt
        flow <- Shared.buildFlowBindChainAsync Shared.RailwayDepth failAt
        fsToolkitAsync <- Shared.buildFsToolkitAsyncResultBindChain Shared.RailwayDepth failAt
        directAsync <- Shared.buildDirectAsyncResultBindChain Shared.RailwayDepth failAt

    [<Benchmark(Baseline = true)>]
    member _.Flow() =
        Shared.runFlowAsync flow

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
    let mutable flow = Flow.succeed 0
    let mutable fsToolkitTask = Shared.buildFsToolkitTaskResultBindChain Shared.RailwayDepth None
    let mutable directTask = Shared.buildDirectTaskResultBindChain Shared.RailwayDepth None

    [<Params(1, 20)>]
    member val FailAt = 1 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        failAt <- Some this.FailAt
        flow <- Shared.buildFlowBindChainTask Shared.RailwayDepth failAt
        fsToolkitTask <- Shared.buildFsToolkitTaskResultBindChain Shared.RailwayDepth failAt
        directTask <- Shared.buildDirectTaskResultBindChain Shared.RailwayDepth failAt

    [<Benchmark(Baseline = true)>]
    member _.Flow() =
        Shared.runFlowTask flow

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
    let flowBindAsync = Shared.buildFlowBindChainAsync Shared.CompositionDepth None
    let flowBindTask = Shared.buildFlowBindChainTask Shared.CompositionDepth None
    let directAsyncBind = Shared.buildDirectAsyncResultBindChain Shared.CompositionDepth None
    let directTaskBind = Shared.buildDirectTaskResultBindChain Shared.CompositionDepth None
    let rawTaskBind = Shared.buildRawTaskBindChain Shared.CompositionDepth

    [<Benchmark(Description = "Flow map x100")>]
    member _.FlowMapX100() =
        Shared.runFlow flowMap

    [<Benchmark(Description = "Flow bind x100")>]
    member _.FlowBindX100() =
        Shared.runFlow flowBind

    [<Benchmark(Description = "Flow bind async x100")>]
    member _.FlowBindAsyncX100() =
        Shared.runFlowAsync flowBindAsync

    [<Benchmark(Baseline = true, Description = "Flow bind task x100")>]
    member _.FlowBindTaskX100() =
        Shared.runFlowTask flowBindTask

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
    let flow = Shared.buildFlowCancellationChain ()
    let directTask = Shared.buildDirectCancellableTaskChain ()

    [<Benchmark(Baseline = true)>]
    member _.Flow() =
        Shared.runFlow flow

    [<Benchmark(Description = "Explicit token Task<Result>")>]
    member _.ExplicitTokenTaskResult() =
        Shared.runCancellableTaskResult directTask

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type CancellableTaskBenchmarks() =
    let cancellableTask = Shared.buildCancellableTaskChain ()
    let directTask = Shared.buildDirectCancellableTaskValueChain ()

    [<Benchmark(Baseline = true)>]
    member _.CancellableTask() =
        Shared.runCancellableTask cancellableTask

    [<Benchmark(Description = "Manual token Task")>]
    member _.ManualTokenTask() =
        Shared.runCancellableTask directTask

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type SynchronousCompletionBenchmarks() =
    let flow =
        Flow.succeed 1
        |> Flow.bind (fun value -> Flow.succeed(value + 1))
        |> Flow.bind (fun value -> Flow.succeed(value * 2))
        |> Flow.map (fun value -> value + 3)

    let valueTaskFlow =
        CandidateValueTaskFlow.succeed 1
        |> CandidateValueTaskFlow.bind (fun value -> CandidateValueTaskFlow.succeed(value + 1))
        |> CandidateValueTaskFlow.bind (fun value -> CandidateValueTaskFlow.succeed(value * 2))
        |> CandidateValueTaskFlow.map (fun value -> value + 3)

    let plyValueTask = Shared.buildPlyValueTaskChain 3

    [<Benchmark(Baseline = true)>]
    member _.Flow() =
        flow.RunSynchronously(0)
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

/// The cost of parallel composition itself: each branch and each traversal item runs in a child scope of the caller.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type ParallelCompositionBenchmarks() =
    let items = [ 1..10_000 ]

    let traverse: Flow<unit, string, int> =
        items
        |> Flow.traversePar (Parallelism.bounded 8) (fun value -> Flow.succeed (value + 1))
        |> Flow.map List.length

    let zipPars: Flow<unit, string, int> =
        flow {
            let total = ref 0

            for value in 1..1_000 do
                let! left, right = Flow.zipPar (Flow.succeed value) (Flow.succeed 1)
                total.Value <- total.Value + left + right

            return total.Value
        }

    let sequencePars: Flow<unit, string, int> =
        flow {
            let total = ref 0

            for value in 1..100 do
                let! values = Flow.sequencePar [ for offset in 1..10 -> Flow.succeed (value + offset) ]
                total.Value <- total.Value + List.sum values

            return total.Value
        }

    let races: Flow<unit, string, int> =
        flow {
            let total = ref 0

            for value in 1..1_000 do
                let! won = Flow.race (Flow.succeed value) (Flow.succeed 0)
                total.Value <- total.Value + won

            return total.Value
        }

    [<Benchmark(Description = "traversePar 10k items, parallelism 8")>]
    member _.TraversePar() =
        traverse.RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "zipPar x1000")>]
    member _.ZipPar() =
        zipPars.RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "sequencePar 10 flows x100")>]
    member _.SequencePar() =
        sequencePars.RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "race x1000")>]
    member _.Race() =
        races.RunSynchronously(()) |> Shared.consumeExit

/// Exercises tail recursion separately from prebuilt left-nested composition chains.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type RecursiveLoopBenchmarks() =
    let pause : Flow<unit, string, unit> =
        Flow.fromTask (fun _ -> task { do! System.Threading.Tasks.Task.Yield() })

    let rec synchronous remaining : Flow<unit, string, unit> =
        flow {
            if remaining > 0 then
                do! Flow.ok ()
                return! synchronous (remaining - 1)
        }

    let rec suspending remaining : Flow<unit, string, unit> =
        flow {
            if remaining > 0 then
                do! pause
                return! suspending (remaining - 1)
        }

    let synchronousWhile : Flow<unit, string, unit> =
        flow {
            let mutable remaining = 1000
            while remaining > 0 do
                do! Flow.ok ()
                remaining <- remaining - 1
        }

    let suspendingWhile : Flow<unit, string, unit> =
        flow {
            let mutable remaining = 1000
            while remaining > 0 do
                do! pause
                remaining <- remaining - 1
        }

    [<Benchmark(Description = "Recursive synchronous loop x1000")>]
    member _.RecursiveSynchronous() = synchronous 1000 |> Flow.runSync ()

    [<Benchmark(Description = "While synchronous loop x1000")>]
    member _.WhileSynchronous() = synchronousWhile |> Flow.runSync ()

    [<Benchmark(Description = "Recursive suspending loop x1000")>]
    member _.RecursiveSuspending() = suspending 1000 |> Flow.runSync ()

    [<Benchmark(Description = "While suspending loop x1000")>]
    member _.WhileSuspending() = suspendingWhile |> Flow.runSync ()
