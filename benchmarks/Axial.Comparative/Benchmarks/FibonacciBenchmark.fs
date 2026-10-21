namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures recursive parallel Fibonacci, forking a fiber per branch above a threshold.
[<MemoryDiagnoser>]
[<RankColumn>]
type FibonacciBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<unit, exn, unit> = Unchecked.defaultof<_>

    member _.Ns =
        RuntimeParam.intParams "FIO_BENCH_FIBONACCI_N" [| 25; 30 |]

    member _.Thresholds =
        RuntimeParam.intParams "FIO_BENCH_FIBONACCI_THRESHOLD" [| 12 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("Ns")>]
    member val N = 0 with get, set

    [<ParamsSource("Thresholds")>]
    member val Threshold = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.Fibonacci.effect this.N this.Threshold
        axialEffect <- Axial.Comparative.Axial.Fibonacci.effect this.N this.Threshold

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxial axialEffect
        else RuntimeParam.run runtime effect
