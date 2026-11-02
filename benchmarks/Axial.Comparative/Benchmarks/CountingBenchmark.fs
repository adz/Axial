namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures throughput of a counter actor tallying a stream of increment messages.
[<MemoryDiagnoser>]
[<RankColumn>]
type CountingBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<unit, exn, unit> = Unchecked.defaultof<_>

    member _.MessageCounts =
        RuntimeParam.intParams "FIO_BENCH_COUNTING_MESSAGES" [| 1_000_000 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("MessageCounts")>]
    member val MessageCount = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.Counting.effect this.MessageCount
        axialEffect <- Axial.Comparative.Axial.Counting.effect this.MessageCount

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxial axialEffect
        else RuntimeParam.run runtime effect
