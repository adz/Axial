namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures producer/consumer throughput through a bounded buffer.
[<MemoryDiagnoser>]
[<RankColumn>]
type BoundedBufferBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<unit, exn, unit> = Unchecked.defaultof<_>

    member _.ProducerCounts =
        RuntimeParam.intParams "FIO_BENCH_BOUNDEDBUFFER_PRODUCERS" [| 4 |]

    member _.ConsumerCounts =
        RuntimeParam.intParams "FIO_BENCH_BOUNDEDBUFFER_CONSUMERS" [| 4 |]

    member _.Capacities =
        RuntimeParam.intParams "FIO_BENCH_BOUNDEDBUFFER_CAPACITY" [| 10 |]

    member _.ItemsPerProducerCounts =
        RuntimeParam.intParams "FIO_BENCH_BOUNDEDBUFFER_ITEMS" [| 100_000 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("ProducerCounts")>]
    member val ProducerCount = 0 with get, set

    [<ParamsSource("ConsumerCounts")>]
    member val ConsumerCount = 0 with get, set

    [<ParamsSource("Capacities")>]
    member val Capacity = 0 with get, set

    [<ParamsSource("ItemsPerProducerCounts")>]
    member val ItemsPerProducer = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.BoundedBuffer.effect this.ProducerCount this.ConsumerCount this.Capacity this.ItemsPerProducer
        axialEffect <- Axial.Comparative.Axial.BoundedBuffer.effect this.ProducerCount this.ConsumerCount this.Capacity this.ItemsPerProducer

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxial axialEffect
        else RuntimeParam.run runtime effect
