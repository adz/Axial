namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures the dining-philosophers problem coordinated through an arbitrator actor.
[<MemoryDiagnoser>]
[<RankColumn>]
type PhilosophersBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<unit, exn, unit> = Unchecked.defaultof<_>

    member _.PhilosopherCounts =
        RuntimeParam.intParams "FIO_BENCH_PHILOSOPHERS_COUNT" [| 20 |]

    member _.RoundCounts =
        RuntimeParam.intParams "FIO_BENCH_PHILOSOPHERS_ROUNDS" [| 10_000 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("PhilosopherCounts")>]
    member val PhilosopherCount = 0 with get, set

    [<ParamsSource("RoundCounts")>]
    member val RoundCount = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.Philosophers.effect this.PhilosopherCount this.RoundCount
        axialEffect <- Axial.Comparative.Axial.Philosophers.effect this.PhilosopherCount this.RoundCount

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxial axialEffect
        else RuntimeParam.run runtime effect
