namespace Axial.Benchmarks

open System
open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Axial

/// Small concurrency operations paired with their closest Task control. The controls do not provide Axial's
/// structured scopes, interruption causes, typed errors, observers, or resource ownership.
type FiberCostCase =
    | ForkJoin = 0
    | ForkResource = 1
    | ZipPar2 = 2
    | SequencePar8 = 3
    | SequencePar64 = 4
    | Race = 5
    | TraversePar64 = 6
    | RunOnThreadPool = 7

[<MemoryDiagnoser>]
[<WarmupCount(3)>]
[<IterationCount(8)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type FiberCostBenchmarks() =
    let environment = ClockEnvironment(ManualClock(DateTimeOffset.UnixEpoch))
    let trivial value : Flow<ClockEnvironment, string, int> = Flow.ok value
    let values8 = [ 1..8 ]
    let values64 = [ 1..64 ]

    let resource : Resource<ClockEnvironment, string, int> =
        Resource.create (trivial 1) (fun _ -> Flow.succeed ())

    let forkJoin =
        flow {
            let! fiber = Flow.fork (trivial 1)
            return! Fiber.join fiber
        }

    let forkResource =
        flow {
            let! fiber = Flow.forkResource resource
            return! Fiber.join fiber
        }
        |> Flow.scoped

    let zipPar2 = Flow.zipPar (trivial 1) (trivial 2) |> Flow.map (fun (left, right) -> left + right)
    let sequencePar8 = values8 |> List.map trivial |> Flow.sequencePar |> Flow.map List.sum
    let sequencePar64 = values64 |> List.map trivial |> Flow.sequencePar |> Flow.map List.sum
    let race = Flow.race (trivial 1) (trivial 2)

    let traversePar64 =
        values64
        |> Flow.traversePar (Parallelism.io 64) trivial
        |> Flow.map List.sum

    let runOnThreadPool = trivial 1 |> Flow.runOn RunOn.ThreadPool

    let run (source: Flow<ClockEnvironment, string, int>) =
        source.RunSynchronously(environment) |> Shared.consumeExit

    [<ParamsAllValues>]
    member val Case = FiberCostCase.ForkJoin with get, set

    [<Benchmark>]
    member this.Axial() =
        match this.Case with
        | FiberCostCase.ForkJoin -> run forkJoin
        | FiberCostCase.ForkResource -> run forkResource
        | FiberCostCase.ZipPar2 -> run zipPar2
        | FiberCostCase.SequencePar8 -> run sequencePar8
        | FiberCostCase.SequencePar64 -> run sequencePar64
        | FiberCostCase.Race -> run race
        | FiberCostCase.TraversePar64 -> run traversePar64
        | FiberCostCase.RunOnThreadPool -> run runOnThreadPool
        | unknown -> invalidArg (nameof this.Case) $"Unknown fiber cost case: {unknown}"

    [<Benchmark(Baseline = true)>]
    member this.DotNetControl() =
        let wait (pending: Task<int>) =
            pending.GetAwaiter().GetResult()
        let waitAll (pending: Task<int array>) =
            pending.GetAwaiter().GetResult()

        match this.Case with
        | FiberCostCase.ForkJoin
        | FiberCostCase.ForkResource
        | FiberCostCase.RunOnThreadPool -> Task.Run(fun () -> 1) |> wait
        | FiberCostCase.ZipPar2 ->
            [ Task.Run(fun () -> 1); Task.Run(fun () -> 2) ] |> Task.WhenAll |> waitAll |> Array.sum
        | FiberCostCase.SequencePar8 ->
            values8 |> List.map (fun value -> Task.Run(fun () -> value)) |> Task.WhenAll |> waitAll |> Array.sum
        | FiberCostCase.SequencePar64
        | FiberCostCase.TraversePar64 ->
            values64 |> List.map (fun value -> Task.Run(fun () -> value)) |> Task.WhenAll |> waitAll |> Array.sum
        | FiberCostCase.Race ->
            let left = Task.Run(fun () -> 1)
            let right = Task.Run(fun () -> 2)
            Task.WhenAny(left, right).GetAwaiter().GetResult() |> wait
        | unknown -> invalidArg (nameof this.Case) $"Unknown fiber cost case: {unknown}"
