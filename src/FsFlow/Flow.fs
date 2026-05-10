namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks

module Flow =
    let inline internal invoke
        (flow: Flow<'env, 'error, 'value>)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : Effect<'value, 'error> =
        let (Flow operation) = flow
        operation environment cancellationToken

    /// <summary>Executes a flow with an explicit cancellation token.</summary>
    let runFull (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
        #if FABLE_COMPILER
        invoke flow environment cancellationToken
        #else
        (invoke flow environment cancellationToken).GetAwaiter().GetResult()
        #endif

    /// <summary>Executes a flow with an explicit cancellation token.</summary>
    let runWithToken = runFull

    /// <summary>Executes a flow with the provided environment and the default cancellation token.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.read (fun env -> $"Hello, {env}!")
    /// let result = Flow.run "World" flow
    /// // result = Promise that resolves to Success "Hello, World!" on Fable, or Success "Hello, World!" on .NET
    /// </code>
    /// </example>
    let run (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
        runWithToken environment CancellationToken.None flow

    /// <summary>Creates a successful synchronous flow.</summary>
    let ok (value: 'value) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofValue value)

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 42
    /// let result = Flow.run () flow
    /// // result = Success 42
    /// </code>
    /// </example>
    let succeed (value: 'value) : Flow<'env, 'error, 'value> =
        ok value

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.value "constant"
    /// </code>
    /// </example>
    let value (item: 'value) : Flow<'env, 'error, 'value> =
        succeed item

    /// <summary>Creates a failing synchronous flow.</summary>
    let error (failure: 'error) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofError failure)

    /// <summary>Alias for <see cref="error" /> that reads well in some call sites.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error"
    /// let result = Flow.run () flow
    /// // result = Failure (Cause.Fail "error")
    /// </code>
    /// </example>
    let fail (failure: 'error) : Flow<'env, 'error, 'value> =
        error failure

    /// <summary>Lifts a <see cref="T:System.Result`2" /> into a synchronous flow.</summary>
    /// <example>
    /// <code>
    /// let res = Ok "success"
    /// let flow = Flow.fromResult res
    /// </code>
    /// </example>
    let fromResult (result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofResult result)

    /// <summary>Lifts an option into a synchronous flow with the supplied error.</summary>
    /// <example>
    /// <code>
    /// let opt = Some "value"
    /// let flow = Flow.fromOption "missing" opt
    /// </code>
    /// </example>
    let fromOption (error: 'error) (value: 'value option) : Flow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResult error
        |> fromResult

    /// <summary>Lifts a value option into a synchronous flow with the supplied error.</summary>
    /// <param name="error">The error to return if the value option is <see cref="T:Microsoft.FSharp.Core.FSharpValueOption`1.ValueNone" />.</param>
    /// <param name="value">The value option to lift.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that succeeds with the option's value or fails with the provided error.</returns>
    let fromValueOption (error: 'error) (value: 'value voption) : Flow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    /// <summary>Turns a pure validation result into a synchronous flow with environment-provided failure.</summary>
    /// <remarks>
    /// This helper bridges the gap between pure validation (which often uses <see cref="T:System.Result`2" /> or <see cref="T:FsFlow.Check`1" />)
    /// and the <see cref="T:FsFlow.Flow`3" /> environment model. If the result is an error, the provided <paramref name="errorFlow" />
    /// is executed to produce the final application error.
    /// </remarks>
    /// <param name="errorFlow">A flow that reads the environment to produce an error value.</param>
    /// <param name="result">The pure result to bridge.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that mirrors the success of the result or fails with the outcome of the error flow.</returns>
    let orElseFlow
        (errorFlow: Flow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            match result with
            | Ok value -> EffectFlow.ofValue value
            | Error () ->
                invoke errorFlow environment cancellationToken
                |> EffectFlow.fold EffectFlow.ofError EffectFlow.ofCause)

    /// <summary>Reads the current environment as the flow value.</summary>
    /// <remarks>
    /// Use this when the entire environment object is needed for the next step of the workflow.
    /// For projecting specific properties, <see cref="read" /> is generally more ergonomic.
    /// </remarks>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> whose successful value is the current environment.</returns>
    let env<'env, 'error> : Flow<'env, 'error, 'env> =
        Flow(fun environment _ -> EffectFlow.ofValue environment)

    /// <summary>Projects a value from the current environment.</summary>
    /// <remarks>
    /// This is the primary way to access dependencies or configuration stored in the environment.
    /// The <paramref name="projection" /> function is applied to the environment at execution time.
    /// </remarks>
    /// <param name="projection">A function that extracts a value from the environment.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> containing the projected value.</returns>
    let read (projection: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow(fun environment _ -> EffectFlow.ofValue (projection environment))

    /// <summary>Maps the successful value of a synchronous flow.</summary>
    /// <remarks>
    /// If the source <paramref name="flow" /> fails, the <paramref name="mapper" /> is not executed,
    /// and the error is preserved. This allows for safe transformation of data within the flow.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the successful value.</param>
    /// <param name="flow">The source flow of type <see cref="T:FsFlow.Flow`3" /> to transform.</param>
    /// <returns>A new <see cref="T:FsFlow.Flow`3" /> with the transformed success value of type <c>'next</c>.</returns>
    let map
        (mapper: 'value -> 'next)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> EffectFlow.map mapper)

    /// <summary>Maps the successful value of a synchronous flow to <c>unit</c>.</summary>
    let ignore (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, unit> =
        map (fun _ -> ()) flow

    /// <summary>Sequences a synchronous continuation after a successful value.</summary>
    /// <remarks>
    /// This is the "flatmap" operation for <see cref="T:FsFlow.Flow`3" />. It allows for dependent
    /// steps where the second flow depends on the value produced by the first.
    /// </remarks>
    /// <param name="binder">A function that takes the successful value and returns a new flow.</param>
    /// <param name="flow">The source flow to sequence.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> representing the combined workflow.</returns>
    let bind
        (binder: 'value -> Flow<'env, 'error, 'next>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> EffectFlow.bind (fun value -> invoke (binder value) environment cancellationToken))

    /// <summary>Sequences a synchronous continuation after a successful value.</summary>
    let (>>=)
        (flow: Flow<'env, 'error, 'value>)
        (binder: 'value -> Flow<'env, 'error, 'next>)
        : Flow<'env, 'error, 'next> =
        bind binder flow

    /// <summary>Runs a synchronous side effect on success and preserves the original value.</summary>
    /// <remarks>
    /// Use this for logging, telemetry, or other "fire and forget" operations that should not
    /// alter the primary value path. If the <paramref name="binder" /> flow fails, the entire
    /// flow fails with that error.
    /// </remarks>
    /// <param name="binder">A function that produces a side-effect flow from the successful value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that preserves the original success value after the side effect.</returns>
    let tap
        (binder: 'value -> Flow<'env, 'error, unit>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        bind
            (fun value ->
                binder value
                |> map (fun () -> value))
            flow

    /// <summary>Runs a synchronous side effect on failure and preserves the original error.</summary>
    /// <remarks>
    /// Use this for error logging or cleanup actions that depend on the environment.
    /// If the <paramref name="binder" /> side-effect flow itself fails, its error will
    /// overwrite the original error.
    /// </remarks>
    /// <param name="binder">A function that produces a side-effect flow from the error value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that preserves the original error after the side effect.</returns>
    let tapError
        (binder: 'error -> Flow<'env, 'error, unit>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> EffectFlow.fold
                EffectFlow.ofValue
                (fun cause ->
                    match cause with
                    | Cause.Fail error ->
                        invoke (binder error) environment cancellationToken
                        |> EffectFlow.fold
                            (fun () -> EffectFlow.ofCause cause)
                            EffectFlow.ofCause
                    | _ -> EffectFlow.ofCause cause))

    /// <summary>Maps the error value of a synchronous flow.</summary>
    /// <remarks>
    /// Transforms the error type of the flow while leaving successful values untouched.
    /// Useful for mapping internal errors into public-facing domain errors.
    /// </remarks>
    /// <param name="mapper">The function to transform the error value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> with the transformed error type.</returns>
    let mapError
        (mapper: 'error -> 'nextError)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> EffectFlow.mapError mapper)

    /// <summary>Catches exceptions raised during execution and maps them to a typed error.</summary>
    /// <remarks>
    /// Exceptions that are not caught by this helper will bubble up to the caller of <see cref="run" />.
    /// This ensures that known exceptions can be handled within the flow context.
    /// </remarks>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source flow of type <see cref="T:FsFlow.Flow`3" /> to monitor.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that converts exceptions into success-path errors.</returns>
    let catch
        (handler: exn -> 'error)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            #if FABLE_COMPILER
            invoke flow environment cancellationToken
            |> Promise.catch (handler >> Exit.Failure >> Cause.Fail >> EffectFlow.ofExit)
            #else
            task {
                try
                    return! invoke flow environment cancellationToken
                with error ->
                    return Exit.Failure (Cause.Fail (handler error))
            } |> ValueTask<Exit<'value, 'error>>
            #endif
        )

    /// <summary>Falls back to another flow when the source flow fails.</summary>
    /// <summary>Computes a fallback flow from the source error when the source flow fails.</summary>
    let orElseWith
        (fallback: 'error -> Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> EffectFlow.fold
                EffectFlow.ofValue
                (fun cause ->
                    match cause with
                    | Cause.Fail error -> invoke (fallback error) environment cancellationToken
                    | _ -> EffectFlow.ofCause cause))

    /// <summary>Falls back to another flow when the source flow fails.</summary>
    let orElse
        (fallback: Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        orElseWith (fun _ -> fallback) flow

    /// <summary>Combines two flows into a tuple of their values.</summary>
    let zip
        (left: Flow<'env, 'error, 'left>)
        (right: Flow<'env, 'error, 'right>)
        : Flow<'env, 'error, 'left * 'right> =
        bind
            (fun leftValue ->
                right
                |> map (fun rightValue -> leftValue, rightValue))
            left

    /// <summary>Combines two flows with a mapping function.</summary>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: Flow<'env, 'error, 'left>)
        (right: Flow<'env, 'error, 'right>)
        : Flow<'env, 'error, 'value> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies a flow-wrapped function to a flow-wrapped value.</summary>
    let apply
        (flow: Flow<'env, 'error, 'value -> 'next>)
        (value: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        map2 (fun mapper input -> mapper input) flow value

    /// <summary>Combines three flows with a mapping function.</summary>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'value)
        (left: Flow<'env, 'error, 'left>)
        (middle: Flow<'env, 'error, 'middle>)
        (right: Flow<'env, 'error, 'right>)
        : Flow<'env, 'error, 'value> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

    /// <summary>Maps the successful value of a synchronous flow.</summary>
    let (<!>)
        (mapper: 'value -> 'next)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        map mapper flow

    /// <summary>Applies a flow-wrapped function to a flow-wrapped value.</summary>
    let (<*>)
        (flow: Flow<'env, 'error, 'value -> 'next>)
        (value: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        apply flow value

    /// <summary>Transforms the environment before running the flow.</summary>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: Flow<'innerEnvironment, 'error, 'value>)
        : Flow<'outerEnvironment, 'error, 'value> =
        Flow(fun environment ct ->
            let innerEnvironment = mapping environment
            invoke flow innerEnvironment ct)

    /// <summary>Provides a derived environment from a layer flow to a downstream flow.</summary>
    let provideLayer
        (layer: Flow<'input, 'error, 'environment>)
        (flow: Flow<'environment, 'error, 'value>)
        : Flow<'input, 'error, 'value> =
        Flow(fun environment ct ->
            invoke layer environment ct
            |> EffectFlow.bind (fun innerEnvironment -> invoke flow innerEnvironment ct))

    /// <summary>Defers flow construction until execution time.</summary>
    let delay (factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun environment ct -> invoke (factory ()) environment ct)

    /// <summary>Transforms a sequence of values into a flow and stops at the first failure.</summary>
    let traverse
        (mapping: 'value -> Flow<'env, 'error, 'next>)
        (values: seq<'value>)
        : Flow<'env, 'error, 'next list> =
        Flow(fun environment ct ->
            values
            |> Seq.fold
                (fun effect value ->
                    effect
                    |> EffectFlow.bind (fun results ->
                        invoke (mapping value) environment ct
                        |> EffectFlow.map (fun mapped -> mapped :: results)))
                (EffectFlow.ofValue [])
            |> EffectFlow.map List.rev)

    /// <summary>Transforms a sequence of flows into a flow of a sequence and stops at the first failure.</summary>
    let sequence (flows: seq<Flow<'env, 'error, 'value>>) : Flow<'env, 'error, 'value list> =
        traverse id flows

/// <summary>
/// Core functions for creating, composing, executing, and adapting async flows.
/// </summary>

open System
open System.Threading.Tasks

module internal AsyncFlow =
    /// <summary>Executes an async flow with the provided environment.</summary>
    let run
        (environment: 'env)
        (AsyncFlow operation: AsyncFlow<'env, 'error, 'value>)
        : Async<Exit<'value, 'error>> =
        operation environment

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
        AsyncFlow(fun _ -> async.Return (match result with Ok v -> Exit.Success v | Error e -> Exit.Failure (Cause.Fail e)))

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
                    match Flow.run environment errorFlow with
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
                Flow.invoke flow environment CancellationToken.None
                #else
                Flow.invoke flow environment CancellationToken.None |> _.AsTask() |> Async.AwaitTask
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

/// <summary>
/// Computation expression builder for synchronous <see cref="T:FsFlow.Flow`3" /> workflows.
/// </summary>

open System
open System.Threading
open System.Threading.Tasks
open FsFlow

/// <summary>
/// Represents delayed task work that can observe a runtime cancellation token when it is started.
/// </summary>
/// <typeparam name="value">The type of the produced task value.</typeparam>
type internal ColdTask<'value> =
    | ColdTask of (CancellationToken -> Task<'value>)

module internal TaskFlowExtensions =
    type TaskFlow<'env, 'error, 'value> with
        static member CapabilityService
            (projection: 'env -> 'service)
            : TaskFlow<'env, 'error, 'service> =
            TaskFlow(fun environment _ -> Task.FromResult(Exit.Success(projection environment)))

        static member ServiceFromProvider
            ()
            : TaskFlow<IServiceProvider, MissingCapability, 'service> =
            TaskFlow(fun provider _ ->
                match provider.GetService typeof<'service> with
                | null ->
                    Task.FromResult(
                        Exit.Failure (Cause.Fail
                            {
                                CapabilityType = typeof<'service>
                            }))
                | value -> Task.FromResult(Exit.Success(unbox<'service> value)))

        static member ProvideLayer
            (
                layer: TaskFlow<'input, 'error, 'environment>,
                flow: TaskFlow<'environment, 'error, 'value>
            ) : TaskFlow<'input, 'error, 'value> =
            let (TaskFlow layerOperation) = layer
            let (TaskFlow flowOperation) = flow

            TaskFlow(fun environment cancellationToken ->
                task {
                    let! outcome = layerOperation environment cancellationToken

                    match outcome with
                    | Exit.Success environment -> return! flowOperation environment cancellationToken
                    | Exit.Failure cause -> return Exit.Failure cause
                })

/// <summary>
/// Core functions for creating and executing cold tasks.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal ColdTask =
    let create (operation: CancellationToken -> Task<'value>) : ColdTask<'value> =
        ColdTask operation

    let fromTaskFactory (factory: unit -> Task<'value>) : ColdTask<'value> =
        create (fun _ -> factory ())

    let fromTask (startedTask: Task<'value>) : ColdTask<'value> =
        fromTaskFactory (fun () -> startedTask)

    let fromValueTaskFactory
        (factory: CancellationToken -> ValueTask<'value>)
        : ColdTask<'value> =
        create (fun cancellationToken -> factory cancellationToken |> _.AsTask())

    let fromValueTaskFactoryWithoutCancellation
        (factory: unit -> ValueTask<'value>)
        : ColdTask<'value> =
        create (fun _ -> factory () |> _.AsTask())

    let fromValueTask (startedValueTask: ValueTask<'value>) : ColdTask<'value> =
        let startedTask = startedValueTask.AsTask()
        fromTask startedTask

    let run (cancellationToken: CancellationToken) (ColdTask operation: ColdTask<'value>) : Task<'value> =
        operation cancellationToken

/// <summary>
/// Core functions for creating, composing, executing, and adapting task flows.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal TaskFlow =
    /// <summary>Executes a task flow with the provided environment and cancellation token.</summary>
    /// <param name="environment">The environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <param name="flow">The <see cref="T:FsFlow.TaskFlow`3" /> to execute.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing the <see cref="T:FsFlow.Exit`2" />.</returns>
    let run
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (TaskFlow operation: TaskFlow<'env, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        operation environment cancellationToken

    /// <summary>Runs a task flow against a <see cref="T:FsFlow.RuntimeContext`2" /> and its internal cancellation token.</summary>
    /// <param name="context">The <see cref="T:FsFlow.RuntimeContext`2" /> providing services and cancellation.</param>
    /// <param name="flow">The task flow to run.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> with the final exit value.</returns>
    let runContext
        (context: RuntimeContext<'runtime, 'env>)
        (flow: TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        run context context.CancellationToken flow

    /// <summary>Converts a task flow into a hot <see cref="T:System.Threading.Tasks.Task`1" />.</summary>
    /// <remarks>
    /// This is an alias for <see cref="run" /> that emphasizes the conversion to a standard .NET Task.
    /// </remarks>
    /// <param name="environment">The environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <param name="flow">The task flow to convert.</param>
    /// <returns>A started task.</returns>
    let toTask
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: TaskFlow<'env, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        run environment cancellationToken flow

    /// <summary>Creates a successful task flow.</summary>
    /// <param name="value">The success value of type <c>'value</c>.</param>
    /// <returns>A <see cref="T:FsFlow.TaskFlow`3" /> that always succeeds.</returns>
    let ok (value: 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> Task.FromResult(Exit.Success value))

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    let succeed (value: 'value) : TaskFlow<'env, 'error, 'value> =
        ok value

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    let value (item: 'value) : TaskFlow<'env, 'error, 'value> =
        succeed item

    /// <summary>Creates a failing task flow.</summary>
    /// <param name="error">The failure value of type <c>'error</c>.</param>
    /// <returns>A <see cref="T:FsFlow.TaskFlow`3" /> that always fails.</returns>
    let error (failure: 'error) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> Task.FromResult(Exit.Failure (Cause.Fail failure)))

    /// <summary>Alias for <see cref="error" /> that reads well in some call sites.</summary>
    let fail (failure: 'error) : TaskFlow<'env, 'error, 'value> =
        error failure

    /// <summary>Lifts a standard <see cref="T:System.Result`2" /> into a task flow.</summary>
    /// <param name="result">The result to lift.</param>
    /// <returns>A task flow mirroring the result.</returns>
    let fromResult (result: Result<'value, 'error>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ -> Task.FromResult (match result with Ok v -> Exit.Success v | Error e -> Exit.Failure (Cause.Fail e)))

    /// <summary>Lifts an option into a task flow with the supplied error.</summary>
    /// <param name="error">The error to return if the option is <see cref="T:Microsoft.FSharp.Core.FSharpOption`1.None" />.</param>
    /// <param name="value">The option to lift.</param>
    /// <returns>A task flow succeeding with the option's value or failing.</returns>
    let fromOption (error: 'error) (value: 'value option) : TaskFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResult error
        |> fromResult

    /// <summary>Lifts a value option into a task flow with the supplied error.</summary>
    /// <param name="error">The error of type <c>'error</c> to return if the value option is <see cref="T:Microsoft.FSharp.Core.FSharpValueOption`1.ValueNone" />.</param>
    /// <param name="value">The value option to lift.</param>
    /// <returns>A task flow succeeding with the option's value or failing.</returns>
    let fromValueOption (error: 'error) (value: 'value voption) : TaskFlow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    let orElseTask
        (errorTask: Task<'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! error = errorTask
                    return Exit.Failure (Cause.Fail error)
            })

    /// <summary>Turns a pure validation result into a task flow with task-provided failure.</summary>
    /// <returns>A <see cref="T:FsFlow.TaskFlow`3" /> that mirrors the result or produces the task error.</returns>
    let orElseAsync
        (errorAsync: Async<'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ _ ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! error = errorAsync |> Async.StartAsTask
                    return Exit.Failure (Cause.Fail error)
            })

    let orElseFlow
        (errorFlow: Flow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    match Flow.run environment errorFlow with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    let orElseAsyncFlow
        (errorFlow: AsyncFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! outcome =
                        AsyncFlow.run environment errorFlow
                        |> fun operation -> Async.StartAsTask(operation, cancellationToken = cancellationToken)

                    match outcome with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    let orElseTaskFlow
        (errorFlow: TaskFlow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                match result with
                | Ok value -> return Exit.Success value
                | Error () ->
                    let! outcome = run environment cancellationToken errorFlow

                    match outcome with
                    | Exit.Success error -> return Exit.Failure (Cause.Fail error)
                    | Exit.Failure cause -> return Exit.Failure cause
            })

    let fromFlow (flow: Flow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment _ -> Task.FromResult(Flow.run environment flow))

    let fromAsyncFlow (flow: AsyncFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            AsyncFlow.run environment flow
            |> fun operation -> Async.StartAsTask(operation, cancellationToken = cancellationToken))

    let fromTask (coldTask: ColdTask<'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ cancellationToken ->
            task {
                let! value = ColdTask.run cancellationToken coldTask
                return Exit.Success value
            })

    let fromTaskResult
        (coldTask: ColdTask<Result<'value, 'error>>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun _ cancellationToken ->
            task {
                let! result = ColdTask.run cancellationToken coldTask
                return match result with Ok v -> Exit.Success v | Error e -> Exit.Failure (Cause.Fail e)
            })

    let env<'env, 'error> : TaskFlow<'env, 'error, 'env> =
        TaskFlow(fun environment _ -> Task.FromResult(Exit.Success environment))

    let read (projection: 'env -> 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment _ -> Task.FromResult(Exit.Success(projection environment)))

    /// <summary>Reads the runtime half of a runtime-context environment.</summary>
    let readRuntime
        (projection: 'runtime -> 'value)
        : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value> =
        read (fun context -> projection context.Runtime)

    /// <summary>Reads the application environment half of a runtime-context environment.</summary>
    let readEnvironment
        (projection: 'env -> 'value)
        : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value> =
        read (fun context -> projection context.Environment)

    /// <summary>Maps the successful value of a task flow.</summary>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the success value.</param>
    /// <param name="flow">The source task flow of type <see cref="T:FsFlow.TaskFlow`3" />.</param>
    /// <returns>A new <see cref="T:FsFlow.TaskFlow`3" /> with the transformed success value.</returns>
    let map
        (mapper: 'value -> 'next)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.mapWith
                (fun mapOutcome operation ->
                    task {
                        let! result = operation
                        return mapOutcome result
                    })
                mapper
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Maps the successful value of a task flow to <c>unit</c>.</summary>
    let ignore (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, unit> =
        map (fun _ -> ()) flow

    /// <summary>Sequences a task-flow-producing continuation after a successful value.</summary>
    /// <remarks>
    /// This is the "flatmap" operation for <see cref="T:FsFlow.TaskFlow`3" />. It allows for dependent
    /// asynchronous steps where the second flow depends on the value produced by the first.
    /// </remarks>
    /// <param name="binder">A function that takes the successful value and returns a new task flow.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A new task flow representing the combined workflow.</returns>
    let bind
        (binder: 'value -> TaskFlow<'env, 'error, 'next>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.bindWith
                (fun operation onSuccess onFailure ->
                    task {
                        let! exit = operation

                        match exit with
                        | Exit.Success value -> return! onSuccess value
                        | Exit.Failure cause -> return! onFailure cause
                    })
                (fun (environment, cancellationToken) value -> binder value |> run environment cancellationToken)
                (Exit.Failure >> Task.FromResult)
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Sequences a task-flow-producing continuation after a successful value.</summary>
    let (>>=)
        (flow: TaskFlow<'env, 'error, 'value>)
        (binder: 'value -> TaskFlow<'env, 'error, 'next>)
        : TaskFlow<'env, 'error, 'next> =
        bind binder flow

    /// <summary>Runs a task-based side effect on success and preserves the original value.</summary>
    /// <param name="binder">A function that produces a side-effect task flow from the successful value.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A task flow that preserves the original success value after the side effect.</returns>
    let tap
        (binder: 'value -> TaskFlow<'env, 'error, unit>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        bind
            (fun value ->
                binder value
                |> map (fun () -> value))
            flow

    /// <summary>Runs a task-based side effect on failure and preserves the original error.</summary>
    /// <param name="binder">A function that produces a side-effect task flow from the error value.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A task flow that preserves the original error after the side effect.</returns>
    let tapError
        (binder: 'error -> TaskFlow<'env, 'error, unit>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let! exit = run environment cancellationToken flow

                match exit with
                | Exit.Success value -> return Exit.Success value
                | Exit.Failure cause ->
                    match cause with
                    | Cause.Fail error ->
                        let! tapExit = binder error |> run environment cancellationToken

                        match tapExit with
                        | Exit.Success () -> return Exit.Failure cause
                        | Exit.Failure tapCause -> return Exit.Failure tapCause
                    | _ -> return Exit.Failure cause
            })

    /// <summary>Maps the error value of a task flow.</summary>
    /// <param name="mapper">A function of type <c>'error -> 'nextError</c>.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A new <see cref="T:FsFlow.TaskFlow`3" /> with the transformed error type.</returns>
    let mapError
        (mapper: 'error -> 'nextError)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'nextError, 'value> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.mapErrorWith
                (fun mapOutcome operation ->
                    task {
                        let! exit = operation
                        return mapOutcome exit
                    })
                mapper
                (fun (environment, cancellationToken) -> run environment cancellationToken flow)
                (environment, cancellationToken))

    /// <summary>Catches exceptions raised during execution and maps them to a typed error.</summary>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source task flow.</param>
    /// <returns>A task flow that converts exceptions into success-path errors.</returns>
    let catch
        (handler: exn -> 'error)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                try
                    return! run environment cancellationToken flow
                with error ->
                    return Exit.Failure (Cause.Fail (handler error))
            })

    /// <summary>Falls back to another task flow when the source flow fails.</summary>
    /// <param name="fallback">The fallback flow of type <see cref="T:FsFlow.TaskFlow`3" />.</param>
    /// <param name="flow">The primary task flow.</param>
    /// <returns>A task flow that tries the primary first, then the fallback.</returns>
    /// <summary>Computes a fallback task flow from the source error when the source flow fails.</summary>
    let orElseWith
        (fallback: 'error -> TaskFlow<'env, 'error, 'value>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let! exit = run environment cancellationToken flow

                match exit with
                | Exit.Success value -> return Exit.Success value
                | Exit.Failure (Cause.Fail error) -> return! run environment cancellationToken (fallback error)
                | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>Falls back to another task flow when the source flow fails.</summary>
    let orElse
        (fallback: TaskFlow<'env, 'error, 'value>)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'value> =
        orElseWith (fun _ -> fallback) flow

    /// <summary>Combines two task flows into a tuple of their values.</summary>
    /// <param name="left">The first task flow.</param>
    /// <param name="right">The second task flow.</param>
    /// <returns>A task flow containing a tuple of results.</returns>
    let zip
        (left: TaskFlow<'env, 'error, 'left>)
        (right: TaskFlow<'env, 'error, 'right>)
        : TaskFlow<'env, 'error, 'left * 'right> =
        bind
            (fun leftValue ->
                right
                |> map (fun rightValue -> leftValue, rightValue))
            left

    /// <summary>Combines two task flows with a mapping function.</summary>
    /// <param name="mapper">A function of type <c>'left -> 'right -> 'value</c>.</param>
    /// <param name="left">The first task flow.</param>
    /// <param name="right">The second task flow.</param>
    /// <returns>A task flow with the combined value.</returns>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: TaskFlow<'env, 'error, 'left>)
        (right: TaskFlow<'env, 'error, 'right>)
        : TaskFlow<'env, 'error, 'value> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies a task-flow-wrapped function to a task-flow-wrapped value.</summary>
    let apply
        (flow: TaskFlow<'env, 'error, 'value -> 'next>)
        (value: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        map2 (fun mapper input -> mapper input) flow value

    /// <summary>Combines three task flows with a mapping function.</summary>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'value)
        (left: TaskFlow<'env, 'error, 'left>)
        (middle: TaskFlow<'env, 'error, 'middle>)
        (right: TaskFlow<'env, 'error, 'right>)
        : TaskFlow<'env, 'error, 'value> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

    /// <summary>Maps the successful value of a task flow.</summary>
    let (<!>)
        (mapper: 'value -> 'next)
        (flow: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        map mapper flow

    /// <summary>Applies a task-flow-wrapped function to a task-flow-wrapped value.</summary>
    let (<*>)
        (flow: TaskFlow<'env, 'error, 'value -> 'next>)
        (value: TaskFlow<'env, 'error, 'value>)
        : TaskFlow<'env, 'error, 'next> =
        apply flow value

    /// <summary>Transforms the environment before running a task flow.</summary>
    /// <param name="mapping">A function of type <c>'outerEnvironment -> 'innerEnvironment</c>.</param>
    /// <param name="flow">The task flow expecting <c>'innerEnvironment</c>.</param>
    /// <returns>A task flow that accepts <c>'outerEnvironment</c>.</returns>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: TaskFlow<'innerEnvironment, 'error, 'value>)
        : TaskFlow<'outerEnvironment, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.localEnvWith
                (fun (environment, cancellationToken) innerFlow -> run environment cancellationToken innerFlow)
                (fun (environment, cancellationToken) -> mapping environment, cancellationToken)
                flow
                (environment, cancellationToken))

    /// <summary>Defers task flow construction until execution time.</summary>
    /// <param name="factory">A function of type <c>unit -> TaskFlow&lt;'env, 'error, 'value&gt;</c>.</param>
    /// <returns>A task flow that evaluates the factory only when executed.</returns>
    let delay (factory: unit -> TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            InternalCombinatorCore.delayWith
                (fun (environment, cancellationToken) delayedFlow -> run environment cancellationToken delayedFlow)
                factory
                (environment, cancellationToken))

    /// <summary>Transforms a sequence of values into a task flow and stops at the first failure.</summary>
    /// <param name="mapping">A function of type <c>'value -> TaskFlow&lt;'env, 'error, 'next&gt;</c>.</param>
    /// <param name="values">The input sequence.</param>
    /// <returns>A task flow containing the list of successful results.</returns>
    let traverse
        (mapping: 'value -> TaskFlow<'env, 'error, 'next>)
        (values: seq<'value>)
        : TaskFlow<'env, 'error, 'next list> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let results = ResizeArray()
                let mutable currentFailure = None
                use enumerator = values.GetEnumerator()

                while currentFailure.IsNone && enumerator.MoveNext() do
                    let! outcome = mapping enumerator.Current |> run environment cancellationToken

                    match outcome with
                    | Exit.Success value -> results.Add value
                    | Exit.Failure cause -> currentFailure <- Some cause

                match currentFailure with
                | Some cause -> return Exit.Failure cause
                | None -> return Exit.Success(List.ofSeq results)
            })

    /// <summary>Transforms a sequence of task flows into a task flow of a sequence and stops at the first failure.</summary>
    /// <param name="flows">A sequence of task flows.</param>
    /// <returns>A task flow containing the list of successful results.</returns>
    let sequence (flows: seq<TaskFlow<'env, 'error, 'value>>) : TaskFlow<'env, 'error, 'value list> =
        traverse id flows

    /// <summary>Provides a derived environment from a layer flow to a downstream task flow.</summary>
    let provideLayer
        (layer: TaskFlow<'input, 'error, 'environment>)
        (flow: TaskFlow<'environment, 'error, 'value>)
        : TaskFlow<'input, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let! outcome = run environment cancellationToken layer

                match outcome with
                | Exit.Success environment -> return! run environment cancellationToken (flow |> localEnv (fun _ -> environment))
                | Exit.Failure cause -> return Exit.Failure cause
            })

    /// <summary>
    /// Task-native runtime helpers for operational concerns like logging, timeout, retry, and scoped cleanup.
    /// </summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        /// <summary>Reads the current runtime cancellation token.</summary>
        let cancellationToken<'env, 'error> : TaskFlow<'env, 'error, CancellationToken> =
            TaskFlow(fun _environment cancellationToken -> Task.FromResult(Exit.Success cancellationToken))

        /// <summary>Converts an <see cref="OperationCanceledException" /> into a typed error.</summary>
        let catchCancellation
            (handler: OperationCanceledException -> 'error)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    try
                        return! run environment cancellationToken flow
                    with :? OperationCanceledException as error ->
                        return Exit.Failure (Cause.Fail (handler error))
                })

        /// <summary>Returns a typed error immediately when the runtime token is already canceled.</summary>
        let ensureNotCanceled<'env, 'error> (canceledError: 'error) : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun _environment cancellationToken ->
                if cancellationToken.IsCancellationRequested then
                    Task.FromResult(Exit.Failure (Cause.Fail canceledError))
                else
                    Task.FromResult(Exit.Success ()))

        /// <summary>Suspends the flow for the specified duration while observing cancellation.</summary>
        let sleep<'env, 'error> (delay: TimeSpan) : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun _environment cancellationToken ->
                task {
                    do! Task.Delay(delay, cancellationToken)
                    return Exit.Success ()
                })

        /// <summary>Writes a fixed log message through the environment-provided logger.</summary>
        let log
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (message: string)
            : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun environment _ ->
                writer
                    environment
                    { Level = level
                      Message = message
                      TimestampUtc = DateTimeOffset.UtcNow }

                Task.FromResult(Exit.Success ()))

        /// <summary>Writes a log message computed from the current environment.</summary>
        let logWith
            (writer: 'env -> LogEntry -> unit)
            (level: LogLevel)
            (messageFactory: 'env -> string)
            : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun environment _ ->
                writer
                    environment
                    { Level = level
                      Message = messageFactory environment
                      TimestampUtc = DateTimeOffset.UtcNow }

                Task.FromResult(Exit.Success ()))

        /// <summary>Acquires a resource, uses it, and always runs the release action.</summary>
        let useWithAcquireRelease
            (acquire: TaskFlow<'env, 'error, 'resource>)
            (release: 'resource -> CancellationToken -> Task)
            (useResource: 'resource -> TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            bind
                (fun resource ->
                    TaskFlow(fun environment cancellationToken ->
                        async {
                            let! result =
                                run environment cancellationToken (useResource resource)
                                |> Async.AwaitTask
                                |> Async.Catch

                            do! release resource cancellationToken |> Async.AwaitTask

                            match result with
                            | Choice1Of2 exit -> return exit
                            | Choice2Of2 error -> return raise error
                        }
                        |> fun computation -> Async.StartAsTask(computation, cancellationToken = cancellationToken)))
                acquire

        /// <summary>Fails with the supplied error when the flow does not complete before the timeout.</summary>
        let timeout
            (after: TimeSpan)
            (timeoutError: 'error)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Exit.Failure (Cause.Fail timeoutError)
                    else
                        return! operation
                })

        /// <summary>Returns the supplied success value when the flow times out.</summary>
        let timeoutToOk
            (after: TimeSpan)
            (value: 'value)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return Exit.Success value
                    else
                        return! operation
                })

        /// <summary>Forwards to <see cref="timeout" /> for a typed failure on timeout.</summary>
        let timeoutToError
            (after: TimeSpan)
            (error: 'error)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            timeout after error flow

        /// <summary>Runs a fallback flow when the original flow times out.</summary>
        let timeoutWith
            (after: TimeSpan)
            (fallback: unit -> TaskFlow<'env, 'error, 'value>)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let operation = run environment cancellationToken flow
                    let timeoutTask = Task.Delay after
                    let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                    if obj.ReferenceEquals(completed, timeoutTask) then
                        return! run environment cancellationToken (fallback ())
                    else
                        return! operation
                })

        /// <summary>Retries a flow according to the supplied policy.</summary>
        let retry
            (policy: RetryPolicy<'error>)
            (flow: TaskFlow<'env, 'error, 'value>)
            : TaskFlow<'env, 'error, 'value> =
            if policy.MaxAttempts < 1 then
                invalidArg (nameof policy.MaxAttempts) "RetryPolicy.MaxAttempts must be at least 1."

            let rec loop attempt =
                TaskFlow(fun environment cancellationToken ->
                    task {
                        let! exit = run environment cancellationToken flow

                        match exit with
                        | Exit.Success value -> return Exit.Success value
                        | Exit.Failure (Cause.Fail error) when attempt < policy.MaxAttempts && policy.ShouldRetry error ->
                            let delay = policy.Delay attempt

                            if delay > TimeSpan.Zero then
                                do! Task.Delay(delay, cancellationToken)

                            return! run environment cancellationToken (loop (attempt + 1))
                        | _ ->
                            return exit
                    })

            loop 1
/// <summary>
/// Describes a task-flow program that is built against a runtime context and later executed with a cancellation token.
/// </summary>
/// <typeparam name="runtime">The runtime service type captured by the spec.</typeparam>
/// <typeparam name="env">The application environment type captured by the spec.</typeparam>
/// <typeparam name="error">The error type produced by the task flow.</typeparam>
/// <typeparam name="value">The success type produced by the task flow.</typeparam>
type internal TaskFlowSpec<'runtime, 'env, 'error, 'value> =
    {
        /// <summary>Runtime services to supply when the spec is run.</summary>
        Runtime: 'runtime

        /// <summary>Application dependencies to supply when the spec is run.</summary>
        Environment: 'env

        /// <summary>Builds the task flow that should run against the runtime context.</summary>
        Build: unit -> TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>
    }

/// <summary>Helpers for creating and running <see cref="TaskFlowSpec{runtime, env, error, value}" /> values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal TaskFlowSpec =
    /// <summary>Creates a task-flow spec from runtime services, application dependencies, and a build function.</summary>
    let create
        (runtime: 'runtime)
        (environment: 'env)
        (build: unit -> TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>)
        : TaskFlowSpec<'runtime, 'env, 'error, 'value> =
        {
            Runtime = runtime
            Environment = environment
            Build = build
        }

    /// <summary>Runs the spec with the supplied cancellation token.</summary>
    let run
        (cancellationToken: CancellationToken)
        (spec: TaskFlowSpec<'runtime, 'env, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        let context = RuntimeContext.create spec.Runtime spec.Environment cancellationToken

        spec.Build ()
        |> TaskFlow.run context cancellationToken

/// <summary>Capability helpers for record projections, runtime adapters, and .NET service-provider interop.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Capability =
    /// <summary>Reads a capability from a record-based environment projection.</summary>
    /// <remarks>
    /// Use this at the edge when a workflow already has a record-shaped environment and only
    /// needs one field, not a full cap-set boundary.
    /// </remarks>
    let inline service (projection: 'env -> 'service) : ^flow
        when ^flow : (static member CapabilityService : ('env -> 'service) -> ^flow) =
        (^flow : (static member CapabilityService : ('env -> 'service) -> ^flow) projection)

    /// <summary>Reads a capability from the runtime half of a two-context runtime environment.</summary>
    let runtime
        (projection: 'runtime -> 'service)
        : Flow<RuntimeContext<'runtime, 'env>, 'error, 'service> =
        Flow.read (fun context -> projection context.Runtime)

    /// <summary>Reads a capability from the application half of a two-context runtime environment.</summary>
    let environment
        (projection: 'env -> 'service)
        : Flow<RuntimeContext<'runtime, 'env>, 'error, 'service> =
        Flow.read (fun context -> projection context.Environment)

    /// <summary>Reads a service from <see cref="IServiceProvider" /> and fails when it is not registered.</summary>
    let serviceFromProvider<'service> : Flow<IServiceProvider, MissingCapability, 'service> =
        Flow(fun provider _ ->
            match provider.GetService typeof<'service> with
            | null ->
                EffectFlow.ofError
                    {
                        CapabilityType = typeof<'service>
                    }
            | value -> EffectFlow.ofValue (unbox<'service> value))

/// <summary>Helpers for deriving an environment in one flow and consuming it in another.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Layer =
    /// <summary>Provides a derived environment from a layer flow to a downstream flow.</summary>
    let inline provideLayer (layer: ^layer) (flow: ^flow) : ^flow
        when ^flow : (static member ProvideLayer : ^layer * ^flow -> ^flow) =
        (^flow : (static member ProvideLayer : ^layer * ^flow -> ^flow) (layer, flow))

/// [omit]
/// <exclude/>
type internal TaskFlowBuilder() =
    member _.Return(value: 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.ok value

    member _.Yield(value: obj) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.ok (unbox<'value> value)

    member _.Yield(project: 'env -> 'value) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.read project

    member _.YieldFrom(flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : TaskFlow<'env, 'error, 'value> =
        operation
        |> AsyncFlow.fromAsync
        |> TaskFlow.fromAsyncFlow

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromTaskResult (ColdTask.fromTask operation)

    member _.ReturnFrom(operation: ValueTask<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromTaskResult (ColdTask.fromValueTask operation)

    member _.ReturnFrom(operation: ColdTask<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromTaskResult operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : TaskFlow<'env, 'error, 'value> =
        operation
        |> AsyncFlow.fromAsyncResult
        |> TaskFlow.fromAsyncFlow

    member _.ReturnFrom(flow: AsyncFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromAsyncFlow flow

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromFlow flow

    member _.ReturnFrom(result: Result<'value, 'error>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.fromResult result

    member _.ReturnFrom(option: 'value option) : TaskFlow<'env, unit, 'value> =
        match option with
        | Some value -> TaskFlow.ok value
        | None -> TaskFlow.error ()

    member _.ReturnFrom(option: 'value voption) : TaskFlow<'env, unit, 'value> =
        match option with
        | ValueSome value -> TaskFlow.ok value
        | ValueNone -> TaskFlow.error ()

    member _.Zero() : TaskFlow<'env, 'error, unit> =
        TaskFlow.ok ()

    member _.Bind
        (
            flow: TaskFlow<'env, 'error, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        TaskFlow.bind binder flow

    member _.Bind
        (
            _request: Env<'dep>,
            binder: 'dep -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep

                return! TaskFlow.run environment cancellationToken (binder dependency)
            })

    member _.Bind
        (
            _request: Env<'dep>,
            binder: unit -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let _dependency = (environment :> Needs<'dep>).Dep

                return! TaskFlow.run environment cancellationToken (binder ())
            })

    member _.Bind
        (
            request: Env<'dep, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (binder (project dependency))
            })

    member _.Bind
        (
            request: Env<'dep, Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (TaskFlow.fromResult (project dependency) |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, 'value option>,
            binder: 'value -> TaskFlow<'env, unit, 'next>
        ) : TaskFlow<'env, unit, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return!
                    TaskFlow.run environment cancellationToken (
                        project dependency
                        |> OptionFlow.toUnitResult
                        |> TaskFlow.fromResult
                        |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, 'value voption>,
            binder: 'value -> TaskFlow<'env, unit, 'next>
        ) : TaskFlow<'env, unit, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return!
                    TaskFlow.run environment cancellationToken (
                        project dependency
                        |> OptionFlow.toUnitResultValueOption
                        |> TaskFlow.fromResult
                        |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Flow<'env, 'error, 'value>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (project dependency |> TaskFlow.fromFlow |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, AsyncFlow<'env, 'error, 'value>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (project dependency |> TaskFlow.fromAsyncFlow |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, TaskFlow<'env, 'error, 'value>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (project dependency |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Async<'value>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (project dependency |> AsyncFlow.fromAsync |> TaskFlow.fromAsyncFlow |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Async<Result<'value, 'error>>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (project dependency |> AsyncFlow.fromAsyncResult |> TaskFlow.fromAsyncFlow |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, Task<Result<'value, 'error>>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (TaskFlow.fromTaskResult (ColdTask.fromTask (project dependency)) |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, ValueTask<Result<'value, 'error>>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (TaskFlow.fromTaskResult (ColdTask.fromValueTask (project dependency)) |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            request: Env<'dep, ColdTask<Result<'value, 'error>>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next>
        when 'env :> Needs<'dep> =
        TaskFlow(fun environment cancellationToken ->
            task {
                let dependency = (environment :> Needs<'dep>).Dep
                let (Env project) = request

                return! TaskFlow.run environment cancellationToken (project dependency |> TaskFlow.fromTaskResult |> TaskFlow.bind binder)
            })

    member _.Bind
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        flow
        |> TaskFlow.fromAsyncFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: Async<'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsync
        |> TaskFlow.fromAsyncFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: Async<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsyncResult
        |> TaskFlow.fromAsyncFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: Task<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        TaskFlow.fromTaskResult (ColdTask.fromTask operation)
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: ValueTask<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        TaskFlow.fromTaskResult (ColdTask.fromValueTask operation)
        |> TaskFlow.bind binder

    member _.Bind
        (
            operation: ColdTask<Result<'value, 'error>>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        operation
        |> TaskFlow.fromTaskResult
        |> TaskFlow.bind binder

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        flow
        |> TaskFlow.fromFlow
        |> TaskFlow.bind binder

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> TaskFlow<'env, 'error, 'next>
        ) : TaskFlow<'env, 'error, 'next> =
        result
        |> TaskFlow.fromResult
        |> TaskFlow.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> TaskFlow<'env, unit, 'next>
        ) : TaskFlow<'env, unit, 'next> =
        match option with
        | Some value -> binder value
        | None -> TaskFlow.error ()

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> TaskFlow<'env, unit, 'next>
        ) : TaskFlow<'env, unit, 'next> =
        match option with
        | ValueSome value -> binder value
        | ValueNone -> TaskFlow.error ()

    member _.Delay(factory: unit -> TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        TaskFlow.delay factory

    member _.Run(flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: TaskFlow<'env, 'error, unit>,
            second: TaskFlow<'env, 'error, 'value>
        ) : TaskFlow<'env, 'error, 'value> =
        first
        |> TaskFlow.bind (fun () -> second)

    member _.TryWith
        (
            flow: TaskFlow<'env, 'error, 'value>,
            handler: exn -> TaskFlow<'env, 'error, 'value>
        ) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                try
                    return! TaskFlow.run environment cancellationToken flow
                with error ->
                    return! TaskFlow.run environment cancellationToken (handler error)
            })

    member _.TryFinally
        (
            flow: TaskFlow<'env, 'error, 'value>,
            compensation: unit -> unit
        ) : TaskFlow<'env, 'error, 'value> =
        TaskFlow(fun environment cancellationToken ->
            task {
                try
                    return! TaskFlow.run environment cancellationToken flow
                finally
                    compensation ()
            })

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> TaskFlow<'env, 'error, 'value>
        ) : TaskFlow<'env, 'error, 'value>
        when 'resource :> IDisposable =
        this.TryFinally(
            binder resource,
            fun () ->
                if not (isNull (box resource)) then
                    resource.Dispose()
        )

    member this.While
        (
            guard: unit -> bool,
            body: TaskFlow<'env, 'error, unit>
        ) : TaskFlow<'env, 'error, unit> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> TaskFlow<'env, 'error, unit>
        ) : TaskFlow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

[<AutoOpen>]
module internal TaskFlowBuilderExtensions =
    type TaskFlowBuilder with
        member this.ReturnFrom(operation: ValueTask) : TaskFlow<'env, 'error, unit> =
            operation.AsTask()
            |> this.ReturnFrom

        member this.ReturnFrom(operation: ValueTask<'value>) : TaskFlow<'env, 'error, 'value> =
            operation.AsTask()
            |> this.ReturnFrom

        member _.ReturnFrom(operation: Task) : TaskFlow<'env, 'error, unit> =
            TaskFlow(fun _ _ ->
                task {
                    do! operation
                    return Exit.Success ()
                })

        member _.ReturnFrom(operation: Task<'value>) : TaskFlow<'env, 'error, 'value> =
            TaskFlow(fun _ _ ->
                task {
                    let! value = operation
                    return Exit.Success value
                })

        member this.Bind
            (
                operation: Task,
                binder: unit -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                operation: Task<'value>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                operation: ValueTask,
                binder: unit -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                operation: ValueTask<'value>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                request: Env<'dep, Task>,
                binder: unit -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next>
            when 'env :> Needs<'dep> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let dependency = (environment :> Needs<'dep>).Dep
                    let (Env project) = request

                    return!
                        TaskFlow.run environment cancellationToken (
                            this.Bind(project dependency, binder))
                })

        member this.Bind
            (
                request: Env<'dep, Task<'value>>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next>
            when 'env :> Needs<'dep> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let dependency = (environment :> Needs<'dep>).Dep
                    let (Env project) = request

                    return!
                        TaskFlow.run environment cancellationToken (
                            this.Bind(project dependency, binder))
                })

        member this.Bind
            (
                request: Env<'dep, ValueTask>,
                binder: unit -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next>
            when 'env :> Needs<'dep> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let dependency = (environment :> Needs<'dep>).Dep
                    let (Env project) = request

                    return!
                        TaskFlow.run environment cancellationToken (
                            this.Bind(project dependency, binder))
                })

        member this.Bind
            (
                request: Env<'dep, ValueTask<'value>>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next>
            when 'env :> Needs<'dep> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let dependency = (environment :> Needs<'dep>).Dep
                    let (Env project) = request

                    return!
                        TaskFlow.run environment cancellationToken (
                            this.Bind(project dependency, binder))
                })

        member _.ReturnFrom(operation: ColdTask<'value>) : TaskFlow<'env, 'error, 'value> =
            TaskFlow.fromTask operation

        member this.Bind
            (
                operation: ColdTask<'value>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next> =
            operation
            |> this.ReturnFrom
            |> TaskFlow.bind binder

        member this.Bind
            (
                request: Env<'dep, ColdTask<'value>>,
                binder: 'value -> TaskFlow<'env, 'error, 'next>
            ) : TaskFlow<'env, 'error, 'next>
            when 'env :> Needs<'dep> =
            TaskFlow(fun environment cancellationToken ->
                task {
                    let dependency = (environment :> Needs<'dep>).Dep
                    let (Env project) = request

                    return!
                        TaskFlow.run environment cancellationToken (
                            this.Bind(project dependency, binder))
                })
[<AutoOpen>]
module internal TaskBuilders =
    /// <summary>
    /// The .NET <c>taskFlow { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This builder enables using <c>let!</c>, <c>do!</c>, and other standard computation expression 
    /// features with <see cref="T:FsFlow.TaskFlow`3" />.
    /// </para>
    /// <para>
    /// It supports seamless binding to many types:
    /// <list type="bullet">
    /// <item><description><see cref="T:FsFlow.TaskFlow`3" /> (standard flow)</description></item>
    /// <item><description><see cref="T:FsFlow.AsyncFlow`3" /> (lifts to task-based flow)</description></item>
    /// <item><description><see cref="T:FsFlow.Flow`3" /> (lifts synchronous to task-based)</description></item>
    /// <item><description><see cref="T:System.Threading.Tasks.Task`1" /> and <see cref="T:System.Threading.Tasks.Task" /> (auto-wraps in Ok)</description></item>
    /// <item><description><see cref="T:System.Threading.Tasks.ValueTask`1" /> and <see cref="T:System.Threading.Tasks.ValueTask" /> (auto-wraps in Ok)</description></item>
    /// <item><description><see cref="T:System.Result`2" /> (lifts pure result to task-based flow)</description></item>
    /// <item><description><see cref="T:Microsoft.FSharp.Control.FSharpAsync`1" /> (auto-wraps in Ok)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// It also supports <c>Guard.Of</c> and <c>Guard.MapError</c> for inline
    /// check-like sources and existing-error remapping before binding into the flow.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let getUser (id: int) = taskFlow {
    ///     let! db = TaskFlow.read (fun env -> env.Db)
    ///     let! user = db.FindUserAsync(id) |> Guard.Of (UserNotFound id)
    ///     do! Task.Delay(100) // Bind to Task
    ///     return user
    /// }
    /// ```
    /// </example>
    let internal taskFlow = TaskFlowBuilder()
