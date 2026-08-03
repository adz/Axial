open Axial.Constraint
open Axial.Constraint.ConstraintDSL

[<EntryPoint>]
let main _ =
    let name: Constraint<string> = Constraint.all [ present; minLength 3 ]

    "Ada"
    |> guard name
    |> orError "invalid name"
    |> function
        | Ok _ -> 0
        | other -> failwithf "Unexpected constraint probe result: %A" other
