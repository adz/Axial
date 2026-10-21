namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures raw fiber fork/join throughput with no work per fiber.
[<MemoryDiagnoser>]
[<RankColumn>]
type ForkBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<ClockEnvironment, exn, unit> = Unchecked.defaultof<_>

    member _.ActorCounts =
        RuntimeParam.intParams "FIO_BENCH_FORK_ACTORS" [| 1_000; 10_000; 50_000 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("ActorCounts")>]
    member val ActorCount = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.Fork.effect this.ActorCount
        axialEffect <- Axial.Comparative.Axial.Fork.effect this.ActorCount

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxialClock axialEffect
        else RuntimeParam.run runtime effect
