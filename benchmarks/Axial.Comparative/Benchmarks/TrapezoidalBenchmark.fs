namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures parallel trapezoidal integration approximating pi across worker fibers.
[<MemoryDiagnoser>]
[<RankColumn>]
type TrapezoidalBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<unit, exn, unit> = Unchecked.defaultof<_>

    member _.WorkerCounts =
        RuntimeParam.intParams "FIO_BENCH_TRAPEZOIDAL_WORKERS" [| 8; 16 |]

    member _.PointCounts =
        RuntimeParam.intParams "FIO_BENCH_TRAPEZOIDAL_POINTS" [| 1_000_000 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("WorkerCounts")>]
    member val WorkerCount = 0 with get, set

    [<ParamsSource("PointCounts")>]
    member val PointCount = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.Trapezoidal.effect this.WorkerCount this.PointCount
        axialEffect <- Axial.Comparative.Axial.Trapezoidal.effect this.WorkerCount this.PointCount

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxialClock axialEffect
        else RuntimeParam.run runtime effect
