open Axial.Refined

[<EntryPoint>]
let main _ =
    Refine.nonEmptyList [ 42 ]
    |> Result.map NonEmptyList.head
    |> function
        | Ok 42 -> 0
        | other -> failwithf "Unexpected Refined probe result: %A" other
