namespace Axial.ElmishFlows

open System
open Axial
open global.Elmish

/// <summary>Elmish commands backed by Axial workflows.</summary>
/// <remarks>
/// Every command follows Elmish's <c>Cmd.OfAsync.either</c> shape of work first, then callbacks, but uses the
/// parameter order <c>name runtime workflow onSuccess onError</c>: the runtime owns the command's root scope, so it
/// precedes the workflow. Pass the workflow positionally, not piped.
/// </remarks>
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
                            let! outcome = Fiber.awaitExit fiber
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

        /// <summary>Runs a workflow and dispatches either its value or its typed failure.</summary>
        /// <param name="name">The diagnostic name for the command fiber.</param>
        /// <param name="runtime">The runtime that owns the command's root scope.</param>
        /// <param name="workflow">The flow to run, passed positionally.</param>
        /// <param name="onSuccess">Maps a successful value to a message.</param>
        /// <param name="onError">Maps a typed failure to a message.</param>
        /// <returns>An Elmish command. Parameter order is <c>name runtime workflow onSuccess onError</c>.</returns>
        let either name runtime workflow onSuccess onError =
            run name None runtime workflow onSuccess onError

        /// <summary>Runs a workflow that cannot fail with a typed error and dispatches its value.</summary>
        /// <param name="name">The diagnostic name for the command fiber.</param>
        /// <param name="runtime">The runtime that owns the command's root scope.</param>
        /// <param name="workflow">The <c>Flow&lt;'env, Never, 'value&gt;</c> to run, passed positionally.</param>
        /// <param name="onSuccess">Maps a successful value to a message.</param>
        /// <returns>An Elmish command. Parameter order is <c>name runtime workflow onSuccess</c>.</returns>
        let perform name runtime workflow onSuccess =
            run name None runtime workflow onSuccess (fun (error: Never) -> match error with _ -> failwith "unreachable")

        /// <summary>Runs a workflow that succeeds only with <c>unit</c> and dispatches its typed failure.</summary>
        /// <param name="name">The diagnostic name for the command fiber.</param>
        /// <param name="runtime">The runtime that owns the command's root scope.</param>
        /// <param name="workflow">The flow to run, passed positionally.</param>
        /// <param name="onError">Maps a typed failure to a message.</param>
        /// <returns>An Elmish command. Parameter order is <c>name runtime workflow onError</c>.</returns>
        let attempt name runtime workflow onError =
            run name None runtime workflow (fun () -> failwith "unreachable") onError

        /// <summary>Starts a fire-and-forget workflow as a named fiber in the runtime's root scope.</summary>
        /// <remarks>
        /// Like <c>perform</c>, the workflow cannot fail with a typed error: its type is
        /// <c>Flow&lt;'env, Never, unit&gt;</c>. Nothing is dispatched, so there is no success or error message;
        /// a defect is reported to <c>OnDefect</c> and interruption is silent. Parameter order is
        /// <c>name runtime workflow</c>.
        /// </remarks>
        /// <param name="name">The diagnostic name for the command fiber.</param>
        /// <param name="runtime">The runtime that owns the command's root scope.</param>
        /// <param name="workflow">The <c>Flow&lt;'env, Never, unit&gt;</c> to run, passed positionally.</param>
        /// <returns>An Elmish command that dispatches no message.</returns>
        let start name (runtime: ElmishRuntime<'env>) (workflow: Flow<'env, Never, unit>) : Cmd<'msg> =
            [ fun _ ->
                let job : Flow<'env, Never, unit> =
                    flow {
                        let! fiber = Flow.forkNamed name (Flow.annotate "elmish.command" name workflow)
                        let! outcome = Fiber.awaitExit fiber
                        match outcome with
                        | Exit.Success () -> ()
                        | Exit.Failure cause ->
                            if not (List.isEmpty (Cause.defects cause)) then
                                Diagnostics.cause name runtime.Options cause
                            elif not (Cause.isInterrupted cause) then
                                Diagnostics.cause name runtime.Options cause
                    }
                runtime.Enqueue job |> ignore ]

        module Latest =
            /// <summary>Runs a workflow keyed by <paramref name="key" />; starting a new run interrupts the previous one.</summary>
            /// <param name="key">The latest-wins key.</param>
            /// <param name="runtime">The runtime that owns the command's root scope.</param>
            /// <param name="workflow">The flow to run, passed positionally.</param>
            /// <param name="onSuccess">Maps a successful value to a message.</param>
            /// <param name="onError">Maps a typed failure to a message.</param>
            /// <returns>An Elmish command. Parameter order is <c>key runtime workflow onSuccess onError</c>.</returns>
            let either key runtime workflow onSuccess onError =
                run key (Some key) runtime workflow onSuccess onError

            /// <summary>Runs a workflow that cannot fail with a typed error, keyed by <paramref name="key" />.</summary>
            /// <param name="key">The latest-wins key.</param>
            /// <param name="runtime">The runtime that owns the command's root scope.</param>
            /// <param name="workflow">The <c>Flow&lt;'env, Never, 'value&gt;</c> to run, passed positionally.</param>
            /// <param name="onSuccess">Maps a successful value to a message.</param>
            /// <returns>An Elmish command. Parameter order is <c>key runtime workflow onSuccess</c>.</returns>
            let perform key runtime workflow onSuccess =
                run key (Some key) runtime workflow onSuccess (fun (error: Never) -> match error with _ -> failwith "unreachable")

            /// <summary>Runs a workflow that succeeds only with <c>unit</c>, keyed by <paramref name="key" />.</summary>
            /// <param name="key">The latest-wins key.</param>
            /// <param name="runtime">The runtime that owns the command's root scope.</param>
            /// <param name="workflow">The flow to run, passed positionally.</param>
            /// <param name="onError">Maps a typed failure to a message.</param>
            /// <returns>An Elmish command. Parameter order is <c>key runtime workflow onError</c>.</returns>
            let attempt key runtime workflow onError =
                run key (Some key) runtime workflow (fun () -> failwith "unreachable") onError

            /// <summary>Interrupts the running command for <paramref name="key" />, if any, without dispatching.</summary>
            let cancel key (runtime: ElmishRuntime<'env>) : Cmd<'msg> =
                [ fun _ -> runtime.Cancel key ]
