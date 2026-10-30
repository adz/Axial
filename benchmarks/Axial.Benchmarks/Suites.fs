namespace Axial.Benchmarks

open System
open System.Threading
open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process
open Axial.Process.DSL

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type PlatformReadBenchmarks() =
    let environment = ()
    let clock = ManualClock(DateTimeOffset.UnixEpoch)
    let completed : Flow<unit, string, int> = Flow.ok 1

    [<Benchmark>]
    member _.TimeoutX1000() =
        for _ in 1 .. 1000 do
            completed |> Flow.timeout (TimeSpan.FromSeconds 1.) "timed out" |> Flow.withClock clock |> fun workflow -> workflow.RunSynchronously(environment) |> ignore

    [<Benchmark>]
    member _.ClockNowX1000() =
        let workflow = flow {
            for _ in 1 .. 1000 do
                let! _ = Clock.now
                ()
        }
        (workflow |> Flow.withClock clock).RunSynchronously(environment) |> ignore

    [<Benchmark>]
    member _.RetrySuccessX1000() =
        for _ in 1 .. 1000 do
            completed |> Flow.retry (Schedule.recurs 3) |> Flow.withClock clock |> fun workflow -> workflow.RunSynchronously(environment) |> ignore

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
        // benchmark measurement requires a synchronous host boundary.
        |> fun operation -> operation.GetAwaiter().GetResult()
        |> Shared.consumeResult

    [<Benchmark(Description = "AsyncLocal updates x10")>]
    member _.AsyncLocalUpdatesX10() =
        asyncLocalReader 0 Shared.noCancellation
        // benchmark measurement requires a synchronous host boundary.
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

/// CandidateValueTaskFlow omits scopes and runtime context; it measures a smaller contract, not a drop-in backend.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type SynchronousCompletionBenchmarks() =
    let primitive : Flow<int, string, int> = Flow.ok 1
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

    [<Benchmark(Description = "Flow succeed")>]
    member _.PrimitiveFlow() =
        primitive.RunSynchronously(0)
        |> Shared.consumeExit

    [<Benchmark(Description = "Flow succeed ValueTask boundary")>]
    member _.PrimitiveValueTask() =
        primitive.StartAsValueTask(0).GetAwaiter().GetResult()
        |> Shared.consumeExit

    [<Benchmark(Description = "Flow pipeline ValueTask boundary")>]
    member _.PipelineValueTask() =
        flow.StartAsValueTask(0).GetAwaiter().GetResult()
        |> Shared.consumeExit

    [<Benchmark(Description = "Candidate ValueTaskFlow")>]
    member _.CandidateValueTaskFlow() =
        valueTaskFlow
        |> CandidateValueTaskFlow.run 0 Platform.Cancellation.never
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

/// Eight CPU-bound slices, comparing inline composition with the explicit scheduling choices.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(2)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type RunOnCpuBenchmarks() =
    let sliceCount = 8
    let iterations = 2_000_000

    let work seed =
        let mutable value = seed
        for index in 1..iterations do
            value <- (value + index) % 1_000_003
        value

    let environment = ClockEnvironment(ManualClock(DateTimeOffset.UnixEpoch))

    let branches () : Flow<ClockEnvironment, string, int> list =
        [ for seed in 1..sliceCount -> Flow.delay (fun () -> Flow.ok (work seed)) ]

    let run source = source |> Flow.map List.sum |> fun current -> current.RunSynchronously(environment) |> Shared.consumeExit

    [<Benchmark(Baseline = true, Description = "sequencePar CPU x8, inline")>]
    member _.SequenceParInline() = branches () |> Flow.sequencePar |> run

    [<Benchmark(Description = "sequencePar CPU x8, runOn ThreadPool")>]
    member _.SequenceParThreadPool() = branches () |> List.map (Flow.runOn RunOn.ThreadPool) |> Flow.sequencePar |> run

    [<Benchmark(Description = "Flow.fork/join CPU x8")>]
    member _.ForkJoin() =
        flow {
            let! fibers = branches () |> List.map Flow.fork |> Flow.sequence
            let! values = fibers |> List.map Fiber.join |> Flow.sequence
            return List.sum values
        }
        |> fun source -> source.RunSynchronously(environment)
        |> Shared.consumeExit

    [<Benchmark(Description = "Task.Run/WhenAll CPU x8")>]
    member _.TaskRunWhenAll() =
        [| for seed in 1..sliceCount -> Task.Run(fun () -> work seed) |]
        |> Task.WhenAll
        |> fun pending -> pending.GetAwaiter().GetResult()
        |> Array.sum

/// Bounded traversal worker placement: completed and yielding I/O-sized operations plus synchronous CPU work.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type BoundedTraversalBenchmarks() =
    let ten = [ 1..10 ]
    let eight = [ 1..8 ]
    let inlinePolicy = Parallelism.bounded 4
    let threadPoolPolicy = inlinePolicy |> Parallelism.runOn RunOn.ThreadPool
    let adaptivePolicy = Parallelism.adaptive 4

    let consumeUnit (exit: Exit<unit, string>) =
        match exit with
        | Exit.Success() -> 1
        | Exit.Failure _ -> -1

    let completed value : Flow<unit, string, int> = Flow.ok (value + 1)
    let completedUnit _ : Flow<unit, string, unit> = Flow.ok ()

    let yielding value : Flow<unit, string, int> =
        Flow.fromTask (fun _ ->
            task {
                do! Task.Yield()
                return value + 1
            })

    let yieldingUnit _ : Flow<unit, string, unit> =
        Flow.fromTask (fun _ ->
            task {
                do! Task.Yield()
            })

    let cpu value =
        Flow.delay (fun () ->
            let mutable result = value
            for index in 1..350_000 do
                result <- (result + index) % 1_000_003
            Flow.ok result)

    let run policy values mapping =
        values |> Flow.traversePar policy mapping |> Flow.map List.sum |> fun source -> source.RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Baseline = true, Description = "traversePar cheap x10, Inline")>]
    member _.CheapInline() = run inlinePolicy ten completed
    [<Benchmark(Description = "traversePar cheap x10, ThreadPool")>]
    member _.CheapThreadPool() = run threadPoolPolicy ten completed
    [<Benchmark(Description = "traversePar cheap x10, adaptive")>]
    member _.CheapAdaptive() = run adaptivePolicy ten completed

    [<Benchmark(Description = "traversePar waiting x10, Inline")>]
    member _.WaitingInline() = run inlinePolicy ten yielding
    [<Benchmark(Description = "traversePar waiting x10, ThreadPool")>]
    member _.WaitingThreadPool() = run threadPoolPolicy ten yielding
    [<Benchmark(Description = "traversePar waiting x10, adaptive")>]
    member _.WaitingAdaptive() = run adaptivePolicy ten yielding

    [<Benchmark(Description = "traversePar CPU x8, Inline")>]
    member _.CpuInline() = run inlinePolicy eight cpu
    [<Benchmark(Description = "traversePar CPU x8, ThreadPool")>]
    member _.CpuThreadPool() = run threadPoolPolicy eight cpu
    [<Benchmark(Description = "traversePar CPU x8, adaptive")>]
    member _.CpuAdaptive() = run adaptivePolicy eight cpu

/// Scheduling overhead on a trivial successful operation.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type RunOnOverheadBenchmarks() =
    let environment = ClockEnvironment(ManualClock(DateTimeOffset.UnixEpoch))
    let trivial : Flow<ClockEnvironment, string, int> = Flow.ok 1
    let onPool = trivial |> Flow.runOn RunOn.ThreadPool
    let adaptiveInline =
        trivial
        |> Flow.runOn (RunOn.adaptiveWith { Threshold = TimeSpan.MaxValue; Samples = 1 })
    let forkJoin : Flow<ClockEnvironment, string, int> =
        flow {
            let! fiber = Flow.fork trivial
            return! Fiber.join fiber
        }

    [<Benchmark(Baseline = true, Description = "plain trivial Flow")>]
    member _.PlainFlow() = trivial.RunSynchronously(environment) |> Shared.consumeExit

    [<Benchmark(Description = "runOn ThreadPool, trivial Flow")>]
    member _.RunOnThreadPool() = onPool.RunSynchronously(environment) |> Shared.consumeExit

    [<Benchmark(Description = "runOn adaptive decided Inline, trivial Flow")>]
    member _.RunOnAdaptiveInline() = adaptiveInline.RunSynchronously(environment) |> Shared.consumeExit

    [<Benchmark(Description = "Flow.fork/join, trivial Flow")>]
    member _.ForkJoin() = forkJoin.RunSynchronously(environment) |> Shared.consumeExit

    [<Benchmark(Description = "Task.Run, trivial function")>]
    member _.TaskRun() =
        Task.Run(fun () -> 1).GetAwaiter().GetResult()

/// The cost of closing a scope: an empty scope (the allocation-free fast path in the runtime) and scopes that release
/// registered resources.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type ScopeCloseBenchmarks() =
    let emptyScopes: Flow<unit, string, int> =
        flow {
            let total = ref 0

            for value in 1..1_000 do
                let! next = Flow.scoped (Flow.succeed value)
                total.Value <- total.Value + next

            return total.Value
        }

    let finalizerScopes: Flow<unit, string, int> =
        flow {
            let total = ref 0

            for value in 1..1_000 do
                let! next =
                    Flow.scoped (
                        flow {
                            do! ScopeCloseCases.finalizer (fun () -> total.Value <- total.Value + 1)
                            return value
                        }
                    )

                total.Value <- total.Value + next

            return total.Value
        }

    let resourceScopes: Flow<unit, string, int> =
        flow {
            let total = ref 0

            for value in 1..1_000 do
                let! next =
                    Flow.scoped (ScopeCloseCases.acquireRelease value (fun _ -> total.Value <- total.Value + 1))

                total.Value <- total.Value + next

            return total.Value
        }

    [<Benchmark(Description = "scoped, empty close x1000")>]
    member _.EmptyClose() =
        emptyScopes.RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "scoped, one finalizer x1000")>]
    member _.FinalizerClose() =
        finalizerScopes.RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "scoped, acquireRelease x1000")>]
    member _.AcquireReleaseClose() =
        resourceScopes.RunSynchronously(()) |> Shared.consumeExit

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
    member _.RecursiveSynchronous() = (synchronous 1000).RunSynchronously(())

    [<Benchmark(Description = "While synchronous loop x1000")>]
    member _.WhileSynchronous() = synchronousWhile.RunSynchronously(())

    [<Benchmark(Description = "Recursive suspending loop x1000")>]
    member _.RecursiveSuspending() = (suspending 1000).RunSynchronously(())

    [<Benchmark(Description = "While suspending loop x1000")>]
    member _.WhileSuspending() = suspendingWhile.RunSynchronously(())

/// Completed operations and scheduler yields are separate cases; neither includes network or timer latency.
/// Each case constructs a recursive workflow and runs it through its normal blocking boundary. The Async cases
/// include Async-to-Task scheduling, so compare each carrier with its direct baseline rather than treating them as
/// interchangeable executor implementations.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
type InteropCompletionBenchmarks() =
    [<Params(false, true)>]
    member val Suspend = false with get, set

    member private this.Step(value: int) =
        if this.Suspend then
            task {
                do! System.Threading.Tasks.Task.Yield()
                return value + 1
            }
        else
            System.Threading.Tasks.Task.FromResult(value + 1)

    member private this.AsyncStep(value: int) =
        async {
            let! next = this.Step value |> Async.AwaitTask
            return next
        }

    [<Benchmark(Baseline = true, Description = "Task<Result> x20")>]
    member this.DirectTask() =
        let rec consume remaining value =
            task {
                if remaining = 0 then return Ok value
                else
                    let! next = this.Step value
                    return! consume (remaining - 1) next
            }
        // benchmark measurement requires a synchronous host boundary.
        consume 20 0 |> fun pending -> pending.GetAwaiter().GetResult() |> Shared.consumeResult

    [<Benchmark(Description = "Async<Result> x20")>]
    member this.DirectAsync() =
        let rec consume remaining value =
            async {
                if remaining = 0 then return Ok value
                else
                    let! next = this.AsyncStep value
                    return! consume (remaining - 1) next
            }
        // benchmark measurement requires a synchronous host boundary.
        consume 20 0 |> Async.RunSynchronously |> Shared.consumeResult

    [<Benchmark(Description = "Flow Task interop x20")>]
    member this.FlowTask() =
        let rec consume remaining value : Flow<unit, string, int> =
            flow {
                if remaining = 0 then return value
                else
                    let! next = Flow.fromTask (fun _ -> this.Step value)
                    return! consume (remaining - 1) next
            }
        (consume 20 0).RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "Flow ValueTask interop x20")>]
    member this.FlowValueTask() =
        let rec consume remaining value : Flow<unit, string, int> =
            flow {
                if remaining = 0 then return value
                else
                    let! next = Flow.fromValueTask (fun _ -> System.Threading.Tasks.ValueTask<int>(this.Step value))
                    return! consume (remaining - 1) next
            }
        (consume 20 0).RunSynchronously(()) |> Shared.consumeExit

    [<Benchmark(Description = "Flow Async interop x20")>]
    member this.FlowAsync() =
        let rec consume remaining value : Flow<unit, string, int> =
            flow {
                if remaining = 0 then return value
                else
                    let! next = Flow.fromAsync (this.AsyncStep value)
                    return! consume (remaining - 1) next
            }
        (consume 20 0).RunSynchronously(()) |> Shared.consumeExit

[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(3)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type ProcessExecutionBenchmarks() =
    let environment =
        { new IHasProcess with
            member _.Process = Process.live Clock.live FileSystem.live Console.live
          interface IHasClock with
            member _.Clock = Clock.live }

    let start workflow = Flow.startTask environment workflow

    let trueWorkflow = cmd $"true" |> Process.toFlow

    let capture50MbWorkflow =
        shText "head -c 52428800 /dev/zero"
        |> Process.captureAll
        |> Process.toFlow

    let threeStagesWorkflow =
        (shText "printf axial") => (cmd $"cat") => (cmd $"cat")
        |> Process.toFlow

    [<Benchmark(Baseline = true, Description = "Raw Process true start to exit")>]
    member _.RawTrueStartToExit() =
        task {
            use child = new System.Diagnostics.Process()
            child.StartInfo.FileName <- "true"
            child.StartInfo.UseShellExecute <- false
            if not (child.Start()) then failwith "Could not start true."
            do! child.WaitForExitAsync()
            return child.ExitCode
        }

    [<Benchmark(Description = "Raw redirected Process true")>]
    member _.RawRedirectedTrue() =
        task {
            use child = new System.Diagnostics.Process()
            child.StartInfo.FileName <- "true"
            child.StartInfo.UseShellExecute <- false
            child.StartInfo.RedirectStandardInput <- true
            child.StartInfo.RedirectStandardOutput <- true
            child.StartInfo.RedirectStandardError <- true
            if not (child.Start()) then failwith "Could not start true."
            child.StandardInput.Close()
            let stdout = child.StandardOutput.ReadToEndAsync()
            let stderr = child.StandardError.ReadToEndAsync()
            do! child.WaitForExitAsync()
            let! _ = stdout
            let! _ = stderr
            return child.ExitCode
        }

    [<Benchmark(Description = "Axial Process true start to exit")>]
    member _.TrueStartToExit() = start trueWorkflow

    [<Benchmark(Description = "Process capture 50 MB")>]
    member _.Capture50Mb() = start capture50MbWorkflow

    [<Benchmark(Description = "Process pipeline, three stages")>]
    member _.ThreeStagePipeline() = start threeStagesWorkflow

/// Left-nested chains can cross the runtime's stack-guard boundary; measure both sides instead of inferring a
/// constant per-bind cost from a single depth. Direct Task recursion supplies a control with the same arithmetic.
[<MemoryDiagnoser>]
[<InProcess>]
[<WarmupCount(3)>]
[<IterationCount(5)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type CompositionDepthBenchmarks() =
    let mutable workflow : Flow<int, string, int> = Flow.ok 0
    let mutable direct = Shared.buildDirectTaskResultBindChain 20 None

    [<Params(20, 80, 100, 200)>]
    member val Depth = 20 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        workflow <- Shared.buildFlowBindChain this.Depth
        direct <- Shared.buildDirectTaskResultBindChain this.Depth None

    [<Benchmark(Baseline = true, Description = "Task<Result> bind")>]
    member _.DirectTask() = Shared.runTaskResult direct

    [<Benchmark(Description = "Flow left-nested bind")>]
    member _.FlowBind() = Shared.runFlow workflow
