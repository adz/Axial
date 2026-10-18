namespace Axial.Elmish

open System
open Axial
open global.Elmish

[<RequireQualifiedAccess>]
module Cmd =
    module OfFlow =
        let private render (value: 'error) =
            match box value with
            | null -> "null"
            | instance -> instance.ToString()

        let private run name (latest: string option) (runtime: ElmishRuntime<'env>) (workflow: Flow<'env, 'error, 'value>) onSuccess onError : Cmd<'msg> =
            [ fun dispatch ->
                let version = latest |> Option.map runtime.Begin
                let current () =
                    match latest, version with
                    | Some key, Some v -> runtime.IsCurrent(key, v)
                    | _ -> true
                let deliver message =
                    if current () then
                        runtime.Options.Post(fun () -> if current () then dispatch message)
                let job : Flow<'env, Never, unit> =
                    flow {
                        if current () then
                            let! fiber = Flow.forkNamed name (Flow.annotate "elmish.command" name workflow)
                            match latest, version with
                            | Some key, Some v -> runtime.SetCurrent(key, v, fiber.Metadata.Id)
                            | _ -> ()
                            let! outcome = Fiber.await fiber
                            if current () then
                                match outcome with
                                | Exit.Success value -> deliver (onSuccess value)
                                | Exit.Failure(Cause.Fail error) -> deliver (onError error)
                                | Exit.Failure cause when Cause.isInterrupted cause -> ()
                                | Exit.Failure cause ->
                                    let error =
                                        match cause with
                                        | Cause.Die ex -> Some ex
                                        | _ -> None
                                    runtime.Options.OnDefect
                                        { Name = name
                                          Error = error
                                          Rendered = Cause.prettyPrint render cause }
                    }
                runtime.Enqueue job |> ignore ]

        let either name runtime workflow onSuccess onError =
            run name None runtime workflow onSuccess onError

        let perform name runtime workflow onSuccess =
            run name None runtime workflow onSuccess (fun (error: Never) -> match error with _ -> failwith "unreachable")

        let attempt name runtime workflow onError =
            run name None runtime workflow (fun () -> failwith "unreachable") onError

        module Latest =
            let either key runtime workflow onSuccess onError =
                run key (Some key) runtime workflow onSuccess onError

            let perform key runtime workflow onSuccess =
                run key (Some key) runtime workflow onSuccess (fun (error: Never) -> failwith "unreachable")

            let attempt key runtime workflow onError =
                run key (Some key) runtime workflow (fun () -> failwith "unreachable") onError

            let cancel key (runtime: ElmishRuntime<'env>) : Cmd<'msg> =
                [ fun _ -> runtime.Cancel key ]
