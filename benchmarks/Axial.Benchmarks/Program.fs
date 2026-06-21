namespace Axial.Benchmarks

open BenchmarkDotNet.Running

module Program =
    [<EntryPoint>]
    let main argv =
        BenchmarkSwitcher.FromAssembly(typeof<ReaderOverheadBenchmarks>.Assembly).Run argv |> ignore
        0
