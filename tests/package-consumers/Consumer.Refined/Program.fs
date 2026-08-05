// Axial.Refined installed alone. Refined depends on Axial.Constraint and nothing else, so this also
// checks that Constraint arrives transitively and that Violation is reachable without naming it.

open Axial.Refined

[<EntryPoint>]
let main _ =
    let populated = NonEmptyList.create [ 1; 2; 3 ]
    let empty = NonEmptyList.create ([]: int list)

    match populated, empty with
    | Ok list, Error _ when list.ToList() = [ 1; 2; 3 ] ->
        printfn "Consumer.Refined OK"
        0
    | other ->
        eprintfn "Consumer.Refined FAILED: %A" other
        1
