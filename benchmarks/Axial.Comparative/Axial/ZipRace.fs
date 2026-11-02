// Mirrors FIO Effects/ZipRace.fs with Flow.zipPar and Flow.race.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal ZipRace =
    let effect rounds : Flow<unit, exn, unit> = flow {
        for round in 1 .. rounds do
            let! _ = Flow.zipPar (Flow.succeed round) (Flow.succeed (round + 1))
            let! _ = Flow.race (Flow.succeed round) (Flow.succeed (round + 1))
            ()
    }
