// Mirrors FIO Effects/Fork.fs. sequencePar starts all no-op flows concurrently.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Fork =
    let effect count : Flow<unit, exn, unit> =
        [ for _ in 1 .. count -> Flow.succeed () ] |> Common.all
