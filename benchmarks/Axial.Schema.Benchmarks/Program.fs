namespace Axial.Schema.Benchmarks

open BenchmarkDotNet.Running

module Program =
    [<EntryPoint>]
    let main argv =
        BenchmarkSwitcher.FromAssembly(typeof<JsonCodecBenchmarks>.Assembly).Run argv |> ignore
        0
