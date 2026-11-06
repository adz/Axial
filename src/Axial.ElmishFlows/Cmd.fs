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
        // onSuccess returns None when success dispatches nothing (attempt), so no callback has to be unreachable.
        let private run
            name
            (latest: string option)
            (runtime: ElmishRuntime<'env>)
            (workflow: Flow<'env, 'error, 'value>)
            (onSuccess: 'value -> 'msg option)
            (onError: 'error -> 'msg)
            : Cmd<'msg> =
            [ fun dispatch ->
                let version = latest |> Option.map runtime.Begin
                let current () =
                    match latest, version with
                    | Some key, Some v -> runtime.IsCurrent(key, v)
                    | _ -> true
                let complete () =
                    match latest, version with
                    | Some key, Some v -> runtime.Complete(key, v)
                    | _ -> ()
                let deliver mapper value =
                    try
                        match mapper value with
                        | Some message ->
                            runtime.Options.Post(fun () ->
                                try
                                    match latest, version with
                                    | Some key, Some v -> runtime.DeliverIfCurrent(key, v, fun () -> dispatch message)
                                    | _ -> dispatch message
                                with error -> Diagnostics.exceptionFrom name runtime.Options error)
                        | None -> complete ()
                    with error ->
                        Diagnostics.exceptionFrom name runtime.Options error
                        complete ()
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
                                | Exit.Failure(Cause.Fail error) -> deliver (onError >> Some) error
                                | Exit.Failure cause ->
                                    Diagnostics.causeUnlessInterrupted name runtime.Options cause
                                    complete ()
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
            run name None runtime workflow (onSuccess >> Some) onError

        /// <summary>Runs a workflow that cannot fail with a typed error and dispatches its value.</summary>
        /// <param name="name">The diagnostic name for the command fiber.</param>
        /// <param name="runtime">The runtime that owns the command's root scope.</param>
        /// <param name="workflow">The <c>Flow&lt;'env, Never, 'value&gt;</c> to run, passed positionally.</param>
        /// <param name="onSuccess">Maps a successful value to a message.</param>
        /// <returns>An Elmish command. Parameter order is <c>name runtime workflow onSuccess</c>.</returns>
        let perform name runtime workflow onSuccess =
            run name None runtime workflow (onSuccess >> Some) Never.absurd

        /// <summary>Runs a workflow that succeeds only with <c>unit</c> and dispatches only its typed failure.</summary>
        /// <remarks>Success dispatches nothing; a defect is reported to <c>OnDefect</c> and interruption is silent.</remarks>
        /// <param name="name">The diagnostic name for the command fiber.</param>
        /// <param name="runtime">The runtime that owns the command's root scope.</param>
        /// <param name="workflow">The flow to run, passed positionally.</param>
        /// <param name="onError">Maps a typed failure to a message.</param>
        /// <returns>An Elmish command. Parameter order is <c>name runtime workflow onError</c>.</returns>
        let attempt name runtime workflow onError =
            run name None runtime workflow (fun () -> None) onError

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
                        | Exit.Failure cause -> Diagnostics.causeUnlessInterrupted name runtime.Options cause
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
                run key (Some key) runtime workflow (onSuccess >> Some) onError

            /// <summary>Runs a workflow that cannot fail with a typed error, keyed by <paramref name="key" />.</summary>
            /// <param name="key">The latest-wins key.</param>
            /// <param name="runtime">The runtime that owns the command's root scope.</param>
            /// <param name="workflow">The <c>Flow&lt;'env, Never, 'value&gt;</c> to run, passed positionally.</param>
            /// <param name="onSuccess">Maps a successful value to a message.</param>
            /// <returns>An Elmish command. Parameter order is <c>key runtime workflow onSuccess</c>.</returns>
            let perform key runtime workflow onSuccess =
                run key (Some key) runtime workflow (onSuccess >> Some) Never.absurd

            /// <summary>Runs a workflow that succeeds only with <c>unit</c>, keyed by <paramref name="key" />.</summary>
            /// <remarks>Success dispatches nothing; only the typed failure becomes a message.</remarks>
            /// <param name="key">The latest-wins key.</param>
            /// <param name="runtime">The runtime that owns the command's root scope.</param>
            /// <param name="workflow">The flow to run, passed positionally.</param>
            /// <param name="onError">Maps a typed failure to a message.</param>
            /// <returns>An Elmish command. Parameter order is <c>key runtime workflow onError</c>.</returns>
            let attempt key runtime workflow onError =
                run key (Some key) runtime workflow (fun () -> None) onError

            /// <summary>Interrupts the running command for <paramref name="key" />, if any, without dispatching.</summary>
            let cancel key (runtime: ElmishRuntime<'env>) : Cmd<'msg> =
                [ fun _ -> runtime.Cancel key ]

    /// <summary>Elmish commands that dispatch every element of an Axial stream.</summary>
    module OfFlowStream =
        /// <summary>
        /// Runs a stream keyed by <paramref name="key" /> and dispatches each element as a message; starting another
        /// stream or <c>Cmd.OfFlow.Latest</c> command for the same key interrupts this one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every element and the final typed failure go through the runtime's <c>Post</c> in stream order, so
        /// <c>update</c> sees them on the host's dispatch thread. The stream has at most one element waiting in
        /// <c>Post</c>: it pulls the next element only after <c>update</c> has seen the previous one, so a fast stream
        /// is slowed to the pace of the dispatcher instead of flooding it, and no element is dropped or merged. Batch a
        /// fast stream with <c>FlowStream.groupedWithin</c> so that one dispatch carries many elements.
        /// </para>
        /// <para>
        /// The element still waiting in <c>Post</c> when the stream is replaced, cancelled or stopped is dropped, and
        /// so is a typed failure still waiting there when the runtime stops. Keys are shared with
        /// <c>Cmd.OfFlow.Latest</c>, so <c>Cmd.OfFlow.Latest.cancel key</c> interrupts the stream without dispatching.
        /// A stream that completes dispatches nothing more; append a final element when <c>update</c> needs to know it
        /// finished. An exception from <paramref name="onElement" /> is reported to <c>OnDefect</c> and that element
        /// is skipped; the stream continues. A defect is reported to <c>OnDefect</c> and interruption is silent.
        /// </para>
        /// </remarks>
        /// <param name="key">The latest-wins key, also the diagnostic name of the stream fiber.</param>
        /// <param name="runtime">The runtime that owns the stream's root scope.</param>
        /// <param name="stream">The stream to run, passed positionally.</param>
        /// <param name="onElement">Maps each element to a message.</param>
        /// <param name="onError">Maps the stream's typed failure to a message.</param>
        /// <returns>An Elmish command. Parameter order is <c>key runtime stream onElement onError</c>.</returns>
        let latest
            (key: string)
            (runtime: ElmishRuntime<'env>)
            (stream: FlowStream<'env, 'error, 'value>)
            (onElement: 'value -> 'msg)
            (onError: 'error -> 'msg)
            : Cmd<'msg> =
            [ fun dispatch ->
                let version = runtime.Begin key
                let report error = Diagnostics.exceptionFrom key runtime.Options error
                // Completion is posted behind the elements so they still find their key current when they run.
                let finish () =
                    try runtime.Options.Post(fun () -> runtime.Complete(key, version))
                    with error ->
                        report error
                        runtime.Complete(key, version)
                // Backpressure: the stream waits until this element's callback has run before pulling the next one.
                let element value : Flow<'env, 'error, unit> =
                    match (try Ok(onElement value) with error -> Error error) with
                    | Error error ->
                        report error
                        Flow.ok ()
                    | Ok message ->
                        flow {
                            let! delivered = Deferred.make<'env, 'error, unit> ()
                            let posted =
                                try
                                    runtime.Options.Post(fun () ->
                                        try
                                            try runtime.DispatchIfCurrent(key, version, fun () -> dispatch message)
                                            with error -> report error
                                        finally
                                            Deferred.succeedNow () delivered |> ignore)
                                    true
                                with error ->
                                    report error
                                    false
                            if posted then do! Deferred.await delivered
                        }
                let job : Flow<'env, Never, unit> =
                    flow {
                        if runtime.IsCurrent(key, version) then
                            let work =
                                stream
                                |> FlowStream.runForEachFlow element
                                |> Flow.annotate "elmish.command" key
                            let! fiber = Flow.forkNamed key work
                            runtime.SetCurrent(key, version, fiber.Metadata.Id)
                            let! outcome = Fiber.awaitExit fiber
                            if runtime.IsCurrent(key, version) then
                                match outcome with
                                | Exit.Success () -> finish ()
                                | Exit.Failure(Cause.Fail error) ->
                                    match (try Ok(onError error) with error -> Error error) with
                                    | Ok message ->
                                        try
                                            runtime.Options.Post(fun () ->
                                                try runtime.DeliverIfCurrent(key, version, fun () -> dispatch message)
                                                with error -> report error)
                                        with error ->
                                            report error
                                            runtime.Complete(key, version)
                                    | Error error ->
                                        report error
                                        finish ()
                                | Exit.Failure cause ->
                                    Diagnostics.causeUnlessInterrupted key runtime.Options cause
                                    finish ()
                    }
                if not (runtime.Enqueue job) then runtime.Complete(key, version) ]
