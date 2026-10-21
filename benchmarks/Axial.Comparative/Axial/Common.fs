namespace Axial.Comparative.Axial

open Axial

[<RequireQualifiedAccess>]
module internal Common =
    let put value queue = Queue.offer value queue |> Flow.ignore
    let all (flows: Flow<unit, exn, unit> list) = Flow.sequencePar flows |> Flow.ignore
    let par values action =
        values |> Seq.map action |> Seq.toList |> Flow.sequencePar |> Flow.ignore
