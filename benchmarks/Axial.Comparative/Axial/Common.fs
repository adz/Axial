namespace Axial.Comparative.Axial

open System
open System.Diagnostics
open System.Threading.Tasks
open Axial

type private BenchmarkClock() =
    let stopwatch = Stopwatch.StartNew()
    interface IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow
        member _.Elapsed() = stopwatch.Elapsed
        member _.Sleep(delay, cancellationToken) = Task.Delay(delay, cancellationToken)

[<RequireQualifiedAccess>]
module internal Common =
    let clockEnvironment = ClockEnvironment(BenchmarkClock())
    let put value queue = Queue.offer value queue |> Flow.ignore
    let all (flows: Flow<unit, exn, unit> list) = Flow.sequencePar flows |> Flow.ignore
    let par values action =
        values |> Seq.map action |> Seq.toList |> Flow.sequencePar |> Flow.ignore
