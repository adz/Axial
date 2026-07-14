open Axial.Flow

type AppError =
    | MissingName

let describeError = function
    | MissingName -> "Pass a name on the command line."

let application : Flow<string array, AppError, string> =
    flow {
        let! arguments = Flow.env

        return!
            match Array.tryHead arguments with
            | Some name -> Flow.succeed $"Hello, {name}."
            | None -> Flow.fail MissingName
    }

[<EntryPoint>]
let main arguments =
    // App owns the root scope; this process edge only renders the final Exit and chooses a code.
    match App.run arguments application |> Async.RunSynchronously with
    | Exit.Success message ->
        printfn "%s" message
        0
    | Exit.Failure cause ->
        eprintfn "%s" (Cause.prettyPrint describeError cause)
        1
