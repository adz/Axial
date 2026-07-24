open Axial.Check
open Axial.Check.CheckDSL

[<EntryPoint>]
let main _ =
    "Ada"
    |> present
    |> Result.bind (minLength 3)
    |> orError "invalid name"
    |> function
        | Ok "Ada" -> 0
        | other -> failwithf "Unexpected Validation probe result: %A" other
