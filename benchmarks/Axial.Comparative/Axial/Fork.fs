// Mirrors FIO Effects/Fork.fs: build inputs, fork every no-op, then join every fiber during each run.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Fork =
    let effect count : Flow<ClockEnvironment, exn, unit> =
        Flow.delay (fun () ->
            flow {
                let items = seq { 1 .. count } |> Seq.toArray
                let mutable fibers = []
                for _ in items do
                    // Fork needs IHasClock for lifecycle timestamps and its scheduler time source.
                    let child: Flow<ClockEnvironment, exn, unit> = Flow.succeed ()
                    let start: Flow<ClockEnvironment, exn, Fiber<exn, unit>> = Flow.fork child
                    let! fiber = start
                    fibers <- fiber :: fibers
                for fiber in List.rev fibers do
                    do! Fiber.join fiber
            })
