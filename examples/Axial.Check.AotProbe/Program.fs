open Axial.Check
open Axial.Check.CheckDSL

[<EntryPoint>]
let main _ =
    "Ada"
    |> Check.all [ present; minLength 3 ]
    |> orError "invalid name"
    |> function
        | Ok () -> 0
        | other -> failwithf "Unexpected Check probe result: %A" other
