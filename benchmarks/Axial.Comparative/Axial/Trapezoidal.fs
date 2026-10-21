// Mirrors FIO Effects/Trapezoidal.fs: defer each slice, fork every slice, then join every fiber.
namespace Axial.Comparative.Axial
open System
open Axial
[<RequireQualifiedAccess>]
module internal Trapezoidal =
    let value workers points : Flow<ClockEnvironment, exn, float> =
        let inner: Flow<ClockEnvironment, exn, float> = flow {
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

            let mutable fibers = []
            for worker in 0 .. workers - 1 do
                // Fork needs IHasClock for lifecycle timestamps and its scheduler time source.
                let start: Flow<ClockEnvironment, exn, Fiber<exn, float>> = Flow.fork (partial worker)
                let! fiber = start
                fibers <- fiber :: fibers

            let mutable values = []
            for fiber in List.rev fibers do
                let! partialValue = Fiber.join fiber
                values <- partialValue :: values

            return List.sum values * h
        }
        inner

    let effect workers points = flow {
        let! result = value workers points
        if abs (result - Math.PI) > 1e-3 then return! Flow.fail (InvalidOperationException $"Trapezoidal: {result} is not close to pi" :> exn)
    }
