module Axial.Comparative.Program

open System
open System.Threading.Tasks
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Engines
open BenchmarkDotNet.Reports
open Perfolizer.Horology
open Axial.Comparative.Benchmarks

let private parseCount name minimum fallback =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> fallback
    | value -> match Int32.TryParse value with true, n when n >= minimum -> n | _ -> invalidArg name $"expected integer >= {minimum}"

let private config () =
    let job = Job.Default.WithWarmupCount(parseCount "FIO_BENCH_WARMUP" 0 3).WithIterationCount(parseCount "FIO_BENCH_ITERATIONS" 1 30).WithMinIterationTime(TimeInterval.FromMilliseconds 100.0).WithStrategy(RunStrategy.Throughput).WithGcServer(true).WithGcConcurrent(true)
    ManualConfig.Create(DefaultConfig.Instance).AddJob(job).WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth 40)

let private validate () = task {
    let cases = [
        "Bang", Fio.Bang.effect 2 3, fun () -> RuntimeParam.runAxial (Axial.Bang.effect 2 3)
        "Big", Fio.Big.effect 3 2, fun () -> RuntimeParam.runAxial (Axial.Big.effect 3 2)
        "BoundedBuffer", Fio.BoundedBuffer.effect 2 2 2 4, fun () -> RuntimeParam.runAxial (Axial.BoundedBuffer.effect 2 2 2 4)
        "Chameneos", Fio.Chameneos.effect 3 5, fun () -> RuntimeParam.runAxial (Axial.Chameneos.effect 3 5)
        "Counting", Fio.Counting.effect 20, fun () -> RuntimeParam.runAxial (Axial.Counting.effect 20)
        "Fibonacci", Fio.Fibonacci.effect 10 5, fun () -> RuntimeParam.runAxialClock (Axial.Fibonacci.effect 10 5)
        "Fork", Fio.Fork.effect 10, fun () -> RuntimeParam.runAxialClock (Axial.Fork.effect 10)
        "Philosophers", Fio.Philosophers.effect 5 3, fun () -> RuntimeParam.runAxial (Axial.Philosophers.effect 5 3)
        "Pingpong", Fio.Pingpong.effect 10, fun () -> RuntimeParam.runAxial (Axial.Pingpong.effect 10)
        "Threadring", Fio.Threadring.effect 5 3, fun () -> RuntimeParam.runAxial (Axial.Threadring.effect 5 3)
        "Trapezoidal", Fio.Trapezoidal.effect 2 10000, fun () -> RuntimeParam.runAxialClock (Axial.Trapezoidal.effect 2 10000)
        "ZipRace", Fio.ZipRace.effect 5, fun () -> RuntimeParam.runAxial (Axial.ZipRace.effect 5) ]
    for name, fioEffect, runAxial in cases do
        use runtime = RuntimeParam.create "Direct"
        let operation = task {
            do! RuntimeParam.run runtime fioEffect
            do! runAxial ()
        }
        let! winner = Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds 30.0))
        if not (obj.ReferenceEquals(winner, operation)) then failwith $"{name}: timed out"
        do! operation
        printfn "PASS %s (FIO Direct, Axial)" name
    use valueRuntime = RuntimeParam.create "Direct"
    let! fioFib = RuntimeParam.runValue valueRuntime (Fio.Validation.fibonacci 10 5)
    let! axialFib = RuntimeParam.runAxialValue () (Axial.Fibonacci.value 10 5 |> global.Axial.Flow.withClock Axial.Common.clock)
    if fioFib <> axialFib || axialFib <> 55 then failwith $"Fibonacci mismatch: FIO={fioFib}, Axial={axialFib}"

    let! fioCount = RuntimeParam.runValue valueRuntime (Fio.Validation.counting 20)
    let! axialCount = RuntimeParam.runAxialValue () (Axial.Counting.value 20)
    if fioCount <> axialCount then failwith $"Counting mismatch: FIO={fioCount}, Axial={axialCount}"

    let! fioTrap = RuntimeParam.runValue valueRuntime (Fio.Validation.trapezoidal 2 10000)
    let! axialTrap = RuntimeParam.runAxialValue () (Axial.Trapezoidal.value 2 10000 |> global.Axial.Flow.withClock Axial.Common.clock)
    if abs (fioTrap - axialTrap) > 1e-12 then failwith $"Trapezoidal mismatch: FIO={fioTrap}, Axial={axialTrap}"
    printfn "PASS values (Fibonacci=%d, Counting=%d, Trapezoidal difference=%g)" axialFib axialCount (abs (fioTrap - axialTrap))
    printfn "Validation passed: 12 workloads"
}

[<EntryPoint>]
let main args =
    if args |> Array.contains "--validate" then
        (validate ()).GetAwaiter().GetResult()
        0
    else
        let hasJob = args |> Array.exists (fun arg -> arg = "--job" || arg.StartsWith("--job=") || arg.StartsWith("-j"))
        let actualConfig = if hasJob then ManualConfig.Create(DefaultConfig.Instance).WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth 40) else config ()
        let summaries = BenchmarkSwitcher.FromTypes([| typeof<BangBenchmark>; typeof<BigBenchmark>; typeof<BoundedBufferBenchmark>; typeof<ChameneosBenchmark>; typeof<CountingBenchmark>; typeof<FibonacciBenchmark>; typeof<ForkBenchmark>; typeof<PhilosophersBenchmark>; typeof<PingpongBenchmark>; typeof<ThreadringBenchmark>; typeof<TrapezoidalBenchmark>; typeof<ZipRaceBenchmark> |]).Run(args, actualConfig)
        if summaries |> Seq.exists (fun summary -> summary.HasCriticalValidationErrors || (summary.Reports |> Seq.exists (fun report -> not report.Success))) then 1 else 0
