// Ported from fio-fsharp/fio benchmarks/FIO.Benchmarks/Effects/Fork.fs @ fe10c67. MIT, Copyright (c) 2022-2026 Daniel Larsen.
[<RequireQualifiedAccess>]
module internal Axial.Comparative.Fio.Fork

open FIO.DSL

// Builds the Fork workload: forks the given number of no-op fibers in parallel.
let effect actorCount : FIO<unit, exn> =
    FIO.forEachParDiscard (seq { 1..actorCount }) (fun _ -> FIO.unit ())
