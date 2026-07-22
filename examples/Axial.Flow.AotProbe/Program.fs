open Axial.Flow

[<EntryPoint>]
let main _ =
    Flow.succeed 21
    |> Flow.map ((*) 2)
    |> fun workflow -> workflow.ToTask(())
    |> fun task -> task.GetAwaiter().GetResult()
    |> function
        | Exit.Success 42 -> 0
        | other -> failwithf "Unexpected Flow probe exit: %A" other
