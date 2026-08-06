open System
open Axial
open Axial.Hosting

type AppError =
    | InvalidDelay of string
    | NonPositiveDelay of int

let describeError = function
    | InvalidDelay value -> $"'{value}' is not a number of seconds."
    | NonPositiveDelay value -> $"'{value}' is not a positive number of seconds."

let application : Flow<string array, AppError, unit> =
    flow {
        let! (arguments: string array) = Flow.env

        let! seconds =
            arguments
            |> Array.tryHead
            |> Option.defaultValue "30"
            |> fun value ->
                match Int32.TryParse value with
                | true, parsed -> Ok parsed
                | false, _ -> Error(InvalidDelay value)
            |> Flow.fromResult

        if seconds <= 0 then
            return! Flow.fail (NonPositiveDelay seconds)

        // DotNetApp does not return an exit code until this root finalizer completes.
        do! Flow.addFinalizerAsync (fun _ -> async { printfn "Root-scope cleanup finished." })
        do! async { printfn "Working for %d seconds. Press Ctrl+C to stop cleanly." seconds }
        do! Flow.Runtime.sleep(TimeSpan.FromSeconds(float seconds))
    }

[<EntryPoint>]
let main arguments =
    // DotNetApp translates Ctrl+C and the structured Exit; F# entry points still require a synchronous int.
    DotNetApp.run describeError arguments application
    |> Async.AwaitTask
    |> Async.RunSynchronously
