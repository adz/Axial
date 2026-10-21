// Mirrors FIO Effects/Fork.fs: build inputs, fork every no-op, then join every fiber during each run.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Fork =
    let effect count : Flow<unit, exn, unit> =
        let inner: Flow<ClockEnvironment, unit, unit> =
            Flow.delay (fun () ->
                flow {
                    let items = [ 1 .. count ]
                    let mutable fibers = []
                    for _ in items do
                        let start: Flow<ClockEnvironment, unit, Fiber<unit, unit>> = Flow.fork (Flow.succeed ())
                        let! fiber = start
                        fibers <- fiber :: fibers
                    for fiber in List.rev fibers do
                        do! Fiber.join fiber
                })
        inner
        |> Flow.mapError (fun () -> System.InvalidOperationException "Fork failed" :> exn)
        |> Flow.localEnv (fun () -> Common.clockEnvironment)
