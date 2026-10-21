// Mirrors FIO Effects/Trapezoidal.fs; partial sums run through Flow.sequencePar.
namespace Axial.Comparative.Axial
open System
open Axial
[<RequireQualifiedAccess>]
module internal Trapezoidal =
    let value workers points : Flow<unit, exn, float> = flow {
        let h = 1.0 / float points
        let chunk = points / workers
        let partial worker =
            let compute () =
                let lo = worker * chunk
                let hi = if worker = workers - 1 then points else (worker + 1) * chunk
                let mutable sum = 0.0
                for i in lo .. (hi - 1) do
                    let x = (float i + 0.5) * h
                    sum <- sum + 4.0 / (1.0 + x * x)
                sum
            Flow.delay (fun () -> Flow.succeed (compute ()))
        let! sums = [ for i in 0 .. workers - 1 -> partial i ] |> Flow.sequencePar
        return List.sum sums * h
    }
    let effect workers points = flow {
        let! result = value workers points
        if abs (result - Math.PI) > 1e-3 then return! Flow.fail (InvalidOperationException $"Trapezoidal: {result} is not close to pi" :> exn)
    }
