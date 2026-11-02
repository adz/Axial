// Mirrors FIO Effects/Fork.fs: build inputs, fork every no-op, then join every fiber during each run.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Fork =
    let effect count : Flow<unit, exn, unit> =
        Flow.delay (fun () ->
            flow {
                let items = seq { 1 .. count } |> Seq.toArray
                let mutable fibers = []
                for _ in items do
                    // The benchmark runner installs its clock around the whole workflow.
                    let child: Flow<unit, exn, unit> = Flow.succeed ()
                    let start: Flow<unit, exn, Fiber<exn, unit>> = Flow.fork child
                    let! fiber = start
                    fibers <- fiber :: fibers
                for fiber in List.rev fibers do
                    do! Fiber.join fiber
            })
