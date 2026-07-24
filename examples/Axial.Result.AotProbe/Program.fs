open Axial.Result

[<EntryPoint>]
let main _ =
    let workflow =
        result {
            let! value = Ok 20
            let! divisor = Ok 2
            return value / divisor
        }

    workflow
    |> Result.map ((+) 1)
    |> Result.orError "invalid workflow"
    |> function
        | Ok 11 -> 0
        | other -> failwithf "Unexpected Result probe result: %A" other
