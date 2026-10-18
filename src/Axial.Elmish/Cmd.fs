namespace Axial.Elmish

open System
open Axial
open global.Elmish

[<RequireQualifiedAccess>]
module Cmd =
    module OfFlow =
        let private run name (latest: string option) (runtime: ElmishRuntime<'env>) (workflow: Flow<'env, 'error, 'value>) onSuccess onError : Cmd<'msg> =
            [ fun dispatch ->
                let version = latest |> Option.map runtime.Begin
                let current () =
                    match latest, version with
                    | Some key, Some v -> runtime.IsCurrent(key, v)
                    | _ -> true
                let deliver mapper value =
                    try
                        let message = mapper value
                        runtime.Options.Post(fun () ->
                            try
                                match latest, version with
                                | Some key, Some v -> runtime.DeliverIfCurrent(key, v, fun () -> dispatch message)
                                | _ -> dispatch message
                            with error -> Diagnostics.exceptionFrom name runtime.Options error)
                    with error ->
                        Diagnostics.exceptionFrom name runtime.Options error
                        match latest, version with
                        | Some key, Some v -> runtime.Complete(key, v)
                        | _ -> ()
                let job : Flow<'env, Never, unit> =
                    flow {
                        if current () then
                            let! fiber = Flow.forkNamed name (Flow.annotate "elmish.command" name workflow)
                            match latest, version with
                            | Some key, Some v -> runtime.SetCurrent(key, v, fiber.Metadata.Id)
                            | _ -> ()
                            let! outcome = Fiber.await fiber
                            let wasCurrent = current ()
                            if wasCurrent then
                                match outcome with
                                | Exit.Success value -> deliver onSuccess value
                                | Exit.Failure(Cause.Fail error) -> deliver onError error
                                | Exit.Failure cause ->
                                    if not (List.isEmpty (Cause.defects cause)) then
                                        Diagnostics.cause name runtime.Options cause
                                    elif not (Cause.isInterrupted cause) then
                                        Diagnostics.cause name runtime.Options cause
                                    match latest, version with
                                    | Some key, Some v -> runtime.Complete(key, v)
                                    | _ -> ()
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
