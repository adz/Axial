open Axial.Validation

[<EntryPoint>]
let main _ =
    Validation.map2 (+) (Validation.ok 20) (Validation.ok 22)
    |> Validation.toResult
    |> function
        | Ok 42 -> 0
        | other -> failwithf "Unexpected Diagnostics probe result: %A" other
