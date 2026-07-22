open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL

[<EntryPoint>]
let main _ =
    "Ada"
    |> present
    |> Result.bind (minLength 3)
    |> Result.orError "invalid name"
    |> function
        | Ok "Ada" -> 0
        | other -> failwithf "Unexpected Validation probe result: %A" other
