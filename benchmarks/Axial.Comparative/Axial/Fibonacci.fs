// Mirrors FIO Effects/Fibonacci.fs using Flow.fork and Fiber.join.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Fibonacci =
    let rec private sequential n = if n < 2 then n else sequential (n - 1) + sequential (n - 2)
    let rec private fib n threshold : Flow<unit, exn, int> =
        if n <= threshold then Flow.succeed (sequential n)
        else
            Flow.zipPar (fib (n - 1) threshold) (fib (n - 2) threshold)
            |> Flow.map (fun (left, right) -> left + right)
    let value n threshold = fib n threshold
    let effect n threshold = fib n threshold |> Flow.ignore
