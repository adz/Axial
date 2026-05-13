namespace FsFlow

#if !FABLE_COMPILER

open System
open System.Threading
open System.Threading.Tasks

module internal AsyncFlow =
    /// <summary>Executes an async flow with the provided environment.</summary>
    /// <remarks>Uncaught exceptions become <c>Cause.Die</c>; cancellation becomes <c>Cause.Interrupt</c>.</remarks>
    let run
        (environment: 'env)
        (AsyncFlow operation: AsyncFlow<'env, 'error, 'value>)
        : Async<Exit<'value, 'error>> =
        async {
            try
                let! exit = operation environment
                return exit
            with error ->
                return Exit.Failure (EffectFlow.causeOfException error)
        }

    /// <summary>Converts an async flow into its raw async result shape.</summary>
    let toAsync (environment: 'env) (flow: AsyncFlow<'env, 'error, 'value>) : Async<Exit<'value, 'error>> =
        run environment flow

    /// <summary>Creates a successful async flow.</summary>
    let ok (value: 'value) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun _ -> async.Return(Exit.Success value))

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    let succeed (value: 'value) : AsyncFlow<'env, 'error, 'value> =
        ok value

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    let value (item: 'value) : AsyncFlow<'env, 'error, 'value> =
        succeed item

    /// <summary>Creates a failing async flow.</summary>
    let error (failure: 'error) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun _ -> async.Return(Exit.Failure (Cause.Fail failure)))

    /// <summary>Alias for <see cref="error" /> that reads well in some call sites.</summary>
    let fail (failure: 'error) : AsyncFlow<'env, 'error, 'value> =
        error failure

    /// <summary>Lifts a <see cref="T:System.Result`2" /> into an async flow.</summary>
    let fromResult (result: Result<'value, 'error>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun _ -> async.Return (Exit.fromResult result))

    /// <summary>Executes an async flow and converts the final <see cref="T:FsFlow.Exit`2" /> into a standard <see cref="T:System.Result`2" />.</summary>
    /// <remarks>
    /// Interruption signals and defects are raised as exceptions in the caller's context.
    /// </remarks>
    let toResult (environment: 'env) (flow: AsyncFlow<'env, 'error, 'value>) : Async<Result<'value, 'error>> =
        async {
            let! exit = run environment flow
            return Exit.toResult exit
        }

    /// <summary>Lifts an option into an async flow with the supplied error.</summary>
    let fromOption (error: 'error) (value: 'value option) : AsyncFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResult error
        |> fromResult

    /// <summary>Lifts a value option into an async flow with the supplied error.</summary>
    let fromValueOption (error: 'error) (value: 'value voption) : AsyncFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    /// <summary>Turns a pure validation result into an async flow with async-provided failure.</summary>
    /// <returns>An <see cref="T:FsFlow.AsyncFlow`3" /> that mirrors the result or produces the async error.</returns>
    let orElseAsync
        (errorAsync: Async<'error>)
        (result: Result<'value, unit>)
        : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun _ ->
            async {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! error = errorAsync
                    return Exit.Failure (Cause.Fail error)
            })

    /// <summary>Turns a pure validation result into an async flow with synchronous environment-provided failure.</summary>
    let orElseFlow
        (errorFlow: Flow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    match FlowInternal.run environment errorFlow with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>Turns a pure validation result into an async flow whose failure value comes from another async flow.</summary>
    let orElseAsyncFlow
        (errorFlow: AsyncFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! outcome = run environment errorFlow

                    match outcome with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>Lifts a synchronous flow into an async flow.</summary>
    let fromFlow (flow: Flow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment -> async {
            let! exit =
                #if FABLE_COMPILER
                FlowInternal.invoke flow environment CancellationToken.None
                #else
                FlowInternal.invoke flow environment CancellationToken.None |> _.AsTask() |> Async.AwaitTask
                #endif
            return exit
        })

    /// <summary>Lifts an async value into an async flow.</summary>
    let fromAsync (operation: Async<'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun _ ->
            async {
                let! value = operation
                return Exit.Success value
            })

    /// <summary>Lifts an async result into an async flow.</summary>
    let fromAsyncResult (operation: Async<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun _ ->
            async {
                let! result = operation
                return match result with Ok v -> Exit.Success v | Error e -> Exit.Failure (Cause.Fail e)
            })

    /// <summary>Reads the current environment as the flow value.</summary>
    let env<'env, 'error> : AsyncFlow<'env, 'error, 'env> =
        AsyncFlow(fun environment -> async.Return(Exit.Success environment))

    /// <summary>Projects a value from the current environment.</summary>
    let read (projection: 'env -> 'value) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment -> async.Return(Exit.Success(projection environment)))

    /// <summary>Maps the successful value of an async flow.</summary>
    let map
        (mapper: 'value -> 'next)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'next> =
        AsyncFlow(
            InternalCombinatorCore.mapWith
                (fun mapOutcome operation ->
                    async {
                        let! exit = operation
                        return mapOutcome exit
                    })
                mapper
                (fun environment -> run environment flow)
        )

    /// <summary>Maps the successful value of an async flow to <c>unit</c>.</summary>
    let ignore (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, unit> =
        map (fun _ -> ()) flow

    /// <summary>Sequences an async continuation after a successful value.</summary>
    let bind
        (binder: 'value -> AsyncFlow<'env, 'error, 'next>)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'next> =
        AsyncFlow(
            InternalCombinatorCore.bindWith
                (fun operation onSuccess onFailure ->
                    async {
                        let! exit = operation

                        match exit with
                        | Exit.Success value -> return! onSuccess value
                        | Exit.Failure cause -> return! onFailure cause
                    })
                (fun environment value -> binder value |> run environment)
                (Exit.Failure >> async.Return)
                (fun environment -> run environment flow)
        )

    /// <summary>Sequences an async continuation after a successful value.</summary>
    let (>>=)
        (flow: AsyncFlow<'env, 'error, 'value>)
        (binder: 'value -> AsyncFlow<'env, 'error, 'next>)
        : AsyncFlow<'env, 'error, 'next> =
        bind binder flow

    /// <summary>Runs an async side effect on success and preserves the original value.</summary>
    let tap
        (binder: 'value -> AsyncFlow<'env, 'error, unit>)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'value> =
        bind
            (fun value ->
                binder value
                |> map (fun () -> value))
            flow

    /// <summary>Runs an async side effect on failure and preserves the original error.</summary>
    let tapError
        (binder: 'error -> AsyncFlow<'env, 'error, unit>)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                let! exit = run environment flow

                match exit with
                | Exit.Success value -> return Exit.Success value
                | Exit.Failure cause ->
                    match cause with
                    | Cause.Fail error ->
                        let! tapExit = binder error |> run environment

                        match tapExit with
                        | Exit.Success () -> return Exit.Failure cause
                        | Exit.Failure tapCause -> return Exit.Failure tapCause
                    | _ -> return Exit.Failure cause
            })

    /// <summary>Maps the error value of an async flow.</summary>
    let mapError
        (mapper: 'error -> 'nextError)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'nextError, 'value> =
        AsyncFlow(
            InternalCombinatorCore.mapErrorWith
                (fun mapOutcome operation ->
                    async {
                        let! exit = operation
                        return mapOutcome exit
                    })
                mapper
                (fun environment -> run environment flow)
        )

    /// <summary>Catches exceptions raised during execution and maps them to a typed error.</summary>
    let catch
        (handler: exn -> 'error)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                try
                    return! run environment flow
                with error ->
                    return Exit.Failure (Cause.Fail (handler error))
            })

    /// <summary>Falls back to another async flow when the source flow fails.</summary>
    /// <summary>Computes a fallback async flow from the source error when the source flow fails.</summary>
    let orElseWith
        (fallback: 'error -> AsyncFlow<'env, 'error, 'value>)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                let! exit = run environment flow

                match exit with
                | Exit.Success value -> return Exit.Success value
                | Exit.Failure (Cause.Fail error) -> return! run environment (fallback error)
                | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>Falls back to another async flow when the source flow fails.</summary>
    let orElse
        (fallback: AsyncFlow<'env, 'error, 'value>)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'value> =
        orElseWith (fun _ -> fallback) flow

    /// <summary>Combines two async flows into a tuple of their values.</summary>
    let zip
        (left: AsyncFlow<'env, 'error, 'left>)
        (right: AsyncFlow<'env, 'error, 'right>)
        : AsyncFlow<'env, 'error, 'left * 'right> =
        bind
            (fun leftValue ->
                right
                |> map (fun rightValue -> leftValue, rightValue))
            left

    /// <summary>Combines two async flows with a mapping function.</summary>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: AsyncFlow<'env, 'error, 'left>)
        (right: AsyncFlow<'env, 'error, 'right>)
        : AsyncFlow<'env, 'error, 'value> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies an async-flow-wrapped function to an async-flow-wrapped value.</summary>
    let apply
        (flow: AsyncFlow<'env, 'error, 'value -> 'next>)
        (value: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'next> =
        map2 (fun mapper input -> mapper input) flow value

    /// <summary>Combines three async flows with a mapping function.</summary>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'value)
        (left: AsyncFlow<'env, 'error, 'left>)
        (middle: AsyncFlow<'env, 'error, 'middle>)
        (right: AsyncFlow<'env, 'error, 'right>)
        : AsyncFlow<'env, 'error, 'value> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

    /// <summary>Maps the successful value of an async flow.</summary>
    let (<!>)
        (mapper: 'value -> 'next)
        (flow: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'next> =
        map mapper flow

    /// <summary>Applies an async-flow-wrapped function to an async-flow-wrapped value.</summary>
    let (<*>)
        (flow: AsyncFlow<'env, 'error, 'value -> 'next>)
        (value: AsyncFlow<'env, 'error, 'value>)
        : AsyncFlow<'env, 'error, 'next> =
        apply flow value

    /// <summary>Transforms the environment before running the async flow.</summary>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: AsyncFlow<'innerEnvironment, 'error, 'value>)
        : AsyncFlow<'outerEnvironment, 'error, 'value> =
        AsyncFlow(InternalCombinatorCore.localEnvWith run mapping flow)

    /// <summary>Provides a derived environment from a layer flow to a downstream flow.</summary>
    let provideLayer
        (layer: AsyncFlow<'input, 'error, 'environment>)
        (flow: AsyncFlow<'environment, 'error, 'value>)
        : AsyncFlow<'input, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                let! outcome = run environment layer

                match outcome with
                | Exit.Success environment -> return! run environment flow
                | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>Defers async flow construction until execution time.</summary>
    let delay (factory: unit -> AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(InternalCombinatorCore.delayWith run factory)

    /// <summary>Transforms a sequence of values into an async flow and stops at the first failure.</summary>
    let traverse
        (mapping: 'value -> AsyncFlow<'env, 'error, 'next>)
        (values: seq<'value>)
        : AsyncFlow<'env, 'error, 'next list> =
        AsyncFlow(fun environment ->
            async {
                let results = ResizeArray()
                let mutable currentFailure = None
                use enumerator = values.GetEnumerator()

                while currentFailure.IsNone && enumerator.MoveNext() do
                    let! outcome = mapping enumerator.Current |> run environment

                    match outcome with
                    | Exit.Success value -> results.Add value
                    | Exit.Failure cause -> currentFailure <- Some cause

                match currentFailure with
                | Some cause -> return Exit.Failure cause
                | None -> return Exit.Success(List.ofSeq results)
            })

    /// <summary>Transforms a sequence of async flows into an async flow of a sequence and stops at the first failure.</summary>
    let sequence (flows: seq<AsyncFlow<'env, 'error, 'value>>) : AsyncFlow<'env, 'error, 'value list> =
        traverse id flows

    /// <summary>
    /// Runtime helpers for operational concerns like logging, timeout, retry, and cleanup.
    /// </summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        open System.Threading
        open System.Threading.Tasks

        /// <summary>
        /// Reads the current cancellation token from the flow.
        /// </summary>
        /// <remarks>This observes the runtime token; it does not translate cancellation into a typed error by itself.</remarks>
        let cancellationToken<'env, 'error> : AsyncFlow<'env, 'error, CancellationToken> =
            AsyncFlow(fun _environment ->
                async {
                    let! cancellationToken = Async.CancellationToken
                    return Exit.Success cancellationToken
                })

        /// <summary>
        /// Catches <see cref="OperationCanceledException"/> and converts it into a typed error.
        /// </summary>
        /// <param name="handler">The function to convert the exception.</param>
        /// <param name="flow">The source flow.</param>
        /// <remarks>This translates cancellation exceptions raised during execution. It does not pre-check the token.</remarks>
        let catchCancellation
            (handler: OperationCanceledException -> 'error)
            (flow: AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            AsyncFlow(fun environment ->
                async {
                    try
                        return! run environment flow
                    with :? OperationCanceledException as error ->
                        return Exit.Failure (Cause.Fail (handler error))
                })

        /// <summary>
        /// Checks if cancellation has been requested and returns a typed error if it has.
        /// </summary>
        /// <param name="canceledError">The error to return if canceled.</param>
        /// <remarks>This observes the current token state and returns a typed error immediately instead of waiting for an exception.</remarks>
        let ensureNotCanceled<'env, 'error> (canceledError: 'error) : AsyncFlow<'env, 'error, unit> =
            AsyncFlow(fun _environment ->
                async {
                    let! cancellationToken = Async.CancellationToken

                    if cancellationToken.IsCancellationRequested then
                        return Exit.Failure (Cause.Fail canceledError)
                    else
                        return Exit.Success ()
                })

        /// <summary>
        /// Suspends the flow for the specified duration, observing cancellation.
        /// </summary>
        /// <param name="delay">The duration to sleep.</param>
        /// <remarks>If the runtime token is canceled, the underlying task raises cancellation which can be translated with <see cref="catchCancellation"/>.</remarks>
        let sleep<'env, 'error> (delay: TimeSpan) : AsyncFlow<'env, 'error, unit> =
            AsyncFlow(fun _environment ->
                async {
                    let! cancellationToken = Async.CancellationToken
                    do! Task.Delay(delay, cancellationToken) |> Async.AwaitTask
                    return Exit.Success ()
                })

        /// <summary>
        /// Writes a log entry using the writer provided by the environment.
        /// </summary>
        /// <param name="writer">The logging function extracted from the environment.</param>
        /// <param name="level">The log level.</param>
        /// <param name="message">The message.</param>
        let log
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (message: string)
            : AsyncFlow<'env, 'error, unit> =
            AsyncFlow(fun environment ->
                async {
                    writer
                        environment
                        { Level = level
                          Message = message
                          TimestampUtc = DateTimeOffset.UtcNow }

                    return Exit.Success ()
                })

        /// <summary>
        /// Writes a log entry using a message produced from the environment.
        /// </summary>
        /// <param name="writer">The logging function extracted from the environment.</param>
        /// <param name="level">The log level.</param>
        /// <param name="messageFactory">The function to produce the message from the environment.</param>
        let logWith
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (messageFactory: 'env -> string)
            : AsyncFlow<'env, 'error, unit> =
            AsyncFlow(fun environment ->
                async {
                    writer
                        environment
                        { Level = level
                          Message = messageFactory environment
                          TimestampUtc = DateTimeOffset.UtcNow }

                    return Exit.Success ()
                })

        /// <summary>
        /// Safely acquires a resource, uses it, and ensures it is released via a task-based action.
        /// </summary>
        /// <param name="acquire">The flow that acquires the resource.</param>
        /// <param name="release">The function that releases the resource.</param>
        /// <param name="useResource">The flow that uses the resource.</param>
        let useWithAcquireRelease
            (acquire: AsyncFlow<'env, 'error, 'resource>)
            (release: 'resource -> CancellationToken -> Task)
            (useResource: 'resource -> AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            bind
                (fun resource ->
                    AsyncFlow(fun environment ->
                        async {
                            let! cancellationToken = Async.CancellationToken
                            let! result = run environment (useResource resource) |> Async.Catch

                            do! release resource cancellationToken |> Async.AwaitTask

                            match result with
                            | Choice1Of2 exit -> return exit
                            | Choice2Of2 error -> return raise error
                        }))
                acquire

        /// <summary>
        /// Wraps a flow with a timeout. If the flow does not complete within the specified duration, returns a typed error.
        /// </summary>
        /// <param name="after">The duration after which to timeout.</param>
        /// <param name="timeoutError">The error to return on timeout.</param>
        /// <param name="flow">The flow to wrap.</param>
        /// <remarks>This helper translates timeout into a typed error. It does not automatically cancel the underlying work on timeout.</remarks>
        let timeout
            (after: TimeSpan)
            (timeoutError: 'error)
            (flow: AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            AsyncFlow(fun environment ->
                async {
                    let! cancellationToken = Async.CancellationToken
                    let operation =
                        run environment flow
                        |> fun asyncOperation -> Async.StartAsTask(asyncOperation, cancellationToken = cancellationToken)

                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |]) |> Async.AwaitTask

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Exit.Failure (Cause.Fail timeoutError)
                    else
                        return! operation |> Async.AwaitTask
                })

        /// <summary>
        /// Wraps a flow with a timeout. If the flow does not complete within the specified duration, returns a success value.
        /// </summary>
        let timeoutToOk
            (after: TimeSpan)
            (value: 'value)
            (flow: AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            AsyncFlow(fun environment ->
                async {
                    let! cancellationToken = Async.CancellationToken
                    let operation =
                        run environment flow
                        |> fun asyncOperation -> Async.StartAsTask(asyncOperation, cancellationToken = cancellationToken)

                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |]) |> Async.AwaitTask

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Exit.Success value
                    else
                        return! operation |> Async.AwaitTask
                })

        /// <summary>
        /// Transitions to a failure value on timeout.
        /// </summary>
        let timeoutToError
            (after: TimeSpan)
            (error: 'error)
            (flow: AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            timeout after error flow

        /// <summary>
        /// Transitions to a fallback workflow on timeout.
        /// </summary>
        let timeoutWith
            (after: TimeSpan)
            (fallback: unit -> AsyncFlow<'env, 'error, 'value>)
            (flow: AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            AsyncFlow(fun environment ->
                async {
                    let! cancellationToken = Async.CancellationToken
                    let operation =
                        run environment flow
                        |> fun asyncOperation -> Async.StartAsTask(asyncOperation, cancellationToken = cancellationToken)

                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |]) |> Async.AwaitTask

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return! run environment (fallback ())
                    else
                        return! operation |> Async.AwaitTask
                })

        /// <summary>
        /// Retries a flow according to the specified policy.
        /// </summary>
        /// <param name="policy">The retry policy.</param>
        /// <param name="flow">The flow to retry.</param>
        let retry
            (policy: RetryPolicy<'error>)
            (flow: AsyncFlow<'env, 'error, 'value>)
            : AsyncFlow<'env, 'error, 'value> =
            if policy.MaxAttempts < 1 then
                invalidArg (nameof policy.MaxAttempts) "RetryPolicy.MaxAttempts must be at least 1."

            let rec loop attempt =
                AsyncFlow(fun environment ->
                    async {
                        let! exit = run environment flow

                        match exit with
                        | Exit.Success value -> return Exit.Success value
                        | Exit.Failure (Cause.Fail error) when attempt < policy.MaxAttempts && policy.ShouldRetry error ->
                            let delay = policy.Delay attempt
                            let! cancellationToken = Async.CancellationToken

                            if delay > TimeSpan.Zero then
                                do! Task.Delay(delay, cancellationToken) |> Async.AwaitTask

                            return! run environment (loop (attempt + 1))
                        | _ ->
                            return exit
                    })

            loop 1

#endif
