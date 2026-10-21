namespace Axial.Comparative.Benchmarks

open FIO.DSL
open Axial
open Axial.Comparative
open Axial.Comparative.Fio

open BenchmarkDotNet.Attributes

open System

// Measures the Chameneos-Redux symmetric rendezvous of color-changing creatures.
[<MemoryDiagnoser>]
[<RankColumn>]
type ChameneosBenchmark() =
    let mutable runtime = Unchecked.defaultof<_>
    let mutable effect = Unchecked.defaultof<_>
    let mutable axialEffect: Flow<unit, exn, unit> = Unchecked.defaultof<_>

    member _.CreatureCounts =
        RuntimeParam.intParams "FIO_BENCH_CHAMENEOS_CREATURES" [| 100 |]

    member _.MeetingCounts =
        RuntimeParam.intParams "FIO_BENCH_CHAMENEOS_MEETINGS" [| 100_000 |]

    member _.Runtimes =
        RuntimeParam.runtimes ()

    [<ParamsSource("CreatureCounts")>]
    member val CreatureCount = 0 with get, set

    [<ParamsSource("MeetingCounts")>]
    member val MeetingCount = 0 with get, set

    [<ParamsSource("Runtimes")>]
    member val Runtime = "" with get, set

    [<GlobalSetup>]
    member this.Setup () =
        if this.Runtime <> "Axial" then runtime <- RuntimeParam.create this.Runtime
        effect <- Axial.Comparative.Fio.Chameneos.effect this.CreatureCount this.MeetingCount
        axialEffect <- Axial.Comparative.Axial.Chameneos.effect this.CreatureCount this.MeetingCount

    [<GlobalCleanup>]
    member _.Cleanup () =
        match box runtime with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    [<Benchmark>]
    member this.Run () =
        if this.Runtime = "Axial" then RuntimeParam.runAxial axialEffect
        else RuntimeParam.run runtime effect
