namespace FsFlow

open System
open System.Threading.Tasks

module Flow =
    /// <summary>Executes a synchronous flow with the provided environment.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.read (fun env -> $"Hello, {env}!")
    /// let result = Flow.run "World" flow
    /// // result = Ok "Hello, World!"
    /// </code>
    /// </example>
    let run (environment: 'env) (Flow operation: Flow<'env, 'error, 'value>) : Result<'value, 'error> =
        operation environment

    /// <summary>Creates a successful synchronous flow.</summary>
    let ok (value: 'value) : Flow<'env, 'error, 'value> =
        Flow(fun _ -> Ok value)

    /// <summary>Alias for <see cref="ok" /> that reads well in some call sites.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 42
    /// let result = Flow.run () flow
    /// // result = Ok 42
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
        Flow(fun _ -> Error failure)

    /// <summary>Alias for <see cref="error" /> that reads well in some call sites.</summary>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error"
    /// let result = Flow.run () flow
    /// // result = Error "error"
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
        Flow(fun _ -> result)

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
        Flow(fun environment ->
            match result with
            | Ok value -> Ok value
            | Error () ->
                match run environment errorFlow with
                | Ok error -> Error error
                | Error error -> Error error)

    /// <summary>Reads the current environment as the flow value.</summary>
    /// <remarks>
    /// Use this when the entire environment object is needed for the next step of the workflow.
    /// For projecting specific properties, <see cref="read" /> is generally more ergonomic.
    /// </remarks>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> whose successful value is the current environment.</returns>
    let env<'env, 'error> : Flow<'env, 'error, 'env> =
        Flow(fun environment -> Ok environment)

    /// <summary>Projects a value from the current environment.</summary>
    /// <remarks>
    /// This is the primary way to access dependencies or configuration stored in the environment.
    /// The <paramref name="projection" /> function is applied to the environment at execution time.
    /// </remarks>
    /// <param name="projection">A function that extracts a value from the environment.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> containing the projected value.</returns>
    let read (projection: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow(fun environment -> Ok(projection environment))

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
        Flow(InternalCombinatorCore.mapWith (fun mapOutcome outcome -> mapOutcome outcome) mapper (fun environment -> run environment flow))

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
        Flow(
            InternalCombinatorCore.bindWith
                (fun outcome onSuccess onError ->
                    match outcome with
                    | Ok value -> onSuccess value
                    | Error error -> onError error)
                (fun environment value -> binder value |> run environment)
                Error
                (fun environment -> run environment flow)
        )

    /// <summary>Sequences a synchronous continuation after a successful value.</summary>
    let inline (>>=)
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
        Flow(fun environment ->
            match run environment flow with
            | Ok value -> Ok value
            | Error error ->
                match binder error |> run environment with
                | Ok () -> Error error
                | Error nextError -> Error nextError)

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
        Flow(
            InternalCombinatorCore.mapErrorWith
                (fun mapOutcome outcome -> mapOutcome outcome)
                mapper
                (fun environment -> run environment flow)
        )

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
        Flow(fun environment ->
            try
                run environment flow
            with error ->
                Error(handler error))

    /// <summary>Falls back to another flow when the source flow fails.</summary>
    /// <summary>Computes a fallback flow from the source error when the source flow fails.</summary>
    let orElseWith
        (fallback: 'error -> Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment ->
            match run environment flow with
            | Ok value -> Ok value
            | Error error -> run environment (fallback error))

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
    let inline (<!>)
        (mapper: 'value -> 'next)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        map mapper flow

    /// <summary>Applies a flow-wrapped function to a flow-wrapped value.</summary>
    let inline (<*>)
        (flow: Flow<'env, 'error, 'value -> 'next>)
        (value: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        apply flow value

    /// <summary>Transforms the environment before running the flow.</summary>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: Flow<'innerEnvironment, 'error, 'value>)
        : Flow<'outerEnvironment, 'error, 'value> =
        Flow(InternalCombinatorCore.localEnvWith run mapping flow)

    /// <summary>Provides a derived environment from a layer flow to a downstream flow.</summary>
    let provideLayer
        (layer: Flow<'input, 'error, 'environment>)
        (flow: Flow<'environment, 'error, 'value>)
        : Flow<'input, 'error, 'value> =
        Flow(fun environment ->
            match run environment layer with
            | Ok environment -> run environment flow
            | Error error -> Error error)

    /// <summary>Defers flow construction until execution time.</summary>
    let delay (factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(InternalCombinatorCore.delayWith run factory)

    /// <summary>Transforms a sequence of values into a flow and stops at the first failure.</summary>
    let traverse
        (mapping: 'value -> Flow<'env, 'error, 'next>)
        (values: seq<'value>)
        : Flow<'env, 'error, 'next list> =
        Flow(fun environment ->
            let results = ResizeArray()
            let mutable currentError = None
            use enumerator = values.GetEnumerator()

            while currentError.IsNone && enumerator.MoveNext() do
                match mapping enumerator.Current |> run environment with
                | Ok value -> results.Add value
                | Error error -> currentError <- Some error

            match currentError with
            | Some error -> Error error
            | None -> Ok(List.ofSeq results))

    /// <summary>Transforms a sequence of flows into a flow of a sequence and stops at the first failure.</summary>
    let sequence (flows: seq<Flow<'env, 'error, 'value>>) : Flow<'env, 'error, 'value list> =
        traverse id flows

/// <summary>
/// Core functions for creating, composing, executing, and adapting async flows.
/// </summary>
