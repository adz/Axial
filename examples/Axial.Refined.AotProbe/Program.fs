open Axial.Refined

[<EntryPoint>]
let main _ =
    Refine.positiveInt 42
    |> Result.map PositiveInt.value
    |> function
        | Ok 42 -> 0
        | other -> failwithf "Unexpected Refined probe result: %A" other
