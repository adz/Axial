// Mirrors FIO Effects/Fibonacci.fs: defer leaves, fork left, evaluate right on this fiber, then join left.
namespace Axial.Comparative.Axial
open System
open Axial
[<RequireQualifiedAccess>]
module internal Fibonacci =
    let rec private sequential n = if n < 2 then n else sequential (n - 1) + sequential (n - 2)
    let rec private fib n threshold : Flow<ClockEnvironment, exn, int> =
        if n <= threshold then
            Flow.delay (fun () -> Flow.succeed (sequential n))
        else
            flow {
                let startLeft: Flow<ClockEnvironment, unit, Fiber<exn, int>> = Flow.fork (fib (n - 1) threshold)
                let! left = startLeft |> Bind.error (InvalidOperationException "Fibonacci fork failed" :> exn)
                let! right = fib (n - 2) threshold
                let! leftValue = Fiber.join left
                return leftValue + right
            }
    let value n threshold = fib n threshold |> Flow.localEnv (fun () -> Common.clockEnvironment)
    let effect n threshold = value n threshold |> Flow.ignore
