namespace FsFlow

open System
open System.Threading.Tasks

type FlowBuilder() =
    member _.Return(value: 'value) : Flow<'env, 'error, 'value> =
        Flow.ok value

    member _.Yield(value: obj) : Flow<'env, 'error, 'value> =
        Flow.ok (unbox<'value> value)

    member _.Yield(project: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow.read project

    member _.YieldFrom(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow.fromResult result

    member _.ReturnFrom(option: 'value option) : Flow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResult
        |> Flow.fromResult

    member _.ReturnFrom(option: 'value voption) : Flow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> Flow.fromResult

    member _.Zero() : Flow<'env, 'error, unit> =
        Flow.ok ()

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        Flow.bind binder flow

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> Flow<'env, 'error, 'next>
        ) : Flow<'env, 'error, 'next> =
        result
        |> Flow.fromResult
        |> Flow.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> Flow<'env, unit, 'next>
        ) : Flow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResult
        |> Flow.fromResult
        |> Flow.bind binder

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> Flow<'env, unit, 'next>
        ) : Flow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> Flow.fromResult
        |> Flow.bind binder

    member _.Delay(factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.delay factory

    member _.Run(flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: Flow<'env, 'error, unit>,
            second: Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value> =
        first
        |> Flow.bind (fun () -> second)

    member _.TryWith
        (
            flow: Flow<'env, 'error, 'value>,
            handler: exn -> Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value> =
        Flow(fun environment ->
            try
                Flow.run environment flow
            with error ->
                Flow.run environment (handler error))

    member _.TryFinally(flow: Flow<'env, 'error, 'value>, compensation: unit -> unit) : Flow<'env, 'error, 'value> =
        Flow(fun environment ->
            try
                Flow.run environment flow
            finally
                compensation ())

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> Flow<'env, 'error, 'value>
        ) : Flow<'env, 'error, 'value>
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
            body: Flow<'env, 'error, unit>
        ) : Flow<'env, 'error, unit> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> Flow<'env, 'error, unit>
        ) : Flow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

/// <summary>
/// Computation expression builder for async <see cref="T:FsFlow.AsyncFlow`3" /> workflows.
/// </summary>
/// <exclude/>
type AsyncFlowBuilder() =
    member _.Return(value: 'value) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.ok value

    member _.Yield(value: obj) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.ok (unbox<'value> value)

    member _.Yield(project: 'env -> 'value) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.read project

    member _.YieldFrom(flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        flow

    member _.ReturnFrom(operation: Async<'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromAsync operation

    member _.ReturnFrom(operation: Async<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromAsyncResult operation

    member _.ReturnFrom(operation: Task<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        operation
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult

    member _.ReturnFrom(operation: ValueTask<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value> =
        operation.AsTask()
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult

    member _.ReturnFrom(flow: Flow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromFlow flow

    member _.ReturnFrom(result: Result<'value, 'error>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.fromResult result

    member _.ReturnFrom(option: 'value option) : AsyncFlow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResult
        |> AsyncFlow.fromResult

    member _.ReturnFrom(option: 'value voption) : AsyncFlow<'env, unit, 'value> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> AsyncFlow.fromResult

    member _.Zero() : AsyncFlow<'env, 'error, unit> =
        AsyncFlow.ok ()

    member _.Bind
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        AsyncFlow.bind binder flow

    member _.Bind
        (
            flow: Flow<'env, 'error, 'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        flow
        |> AsyncFlow.fromFlow
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: Async<'value>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsync
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: Async<Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation
        |> AsyncFlow.fromAsyncResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: Task<Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            operation: ValueTask<Result<'value, 'error>>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        operation.AsTask()
        |> Async.AwaitTask
        |> AsyncFlow.fromAsyncResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> AsyncFlow<'env, 'error, 'next>
        ) : AsyncFlow<'env, 'error, 'next> =
        result
        |> AsyncFlow.fromResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            option: 'value option,
            binder: 'value -> AsyncFlow<'env, unit, 'next>
        ) : AsyncFlow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResult
        |> AsyncFlow.fromResult
        |> AsyncFlow.bind binder

    member _.Bind
        (
            option: 'value voption,
            binder: 'value -> AsyncFlow<'env, unit, 'next>
        ) : AsyncFlow<'env, unit, 'next> =
        option
        |> OptionFlow.toUnitResultValueOption
        |> AsyncFlow.fromResult
        |> AsyncFlow.bind binder

    member _.Delay(factory: unit -> AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow.delay factory

    member _.Run(flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value> =
        flow

    member _.Combine
        (
            first: AsyncFlow<'env, 'error, unit>,
            second: AsyncFlow<'env, 'error, 'value>
        ) : AsyncFlow<'env, 'error, 'value> =
        first
        |> AsyncFlow.bind (fun () -> second)

    member _.TryWith
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            handler: exn -> AsyncFlow<'env, 'error, 'value>
        ) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                try
                    return! AsyncFlow.run environment flow
                with error ->
                    return! AsyncFlow.run environment (handler error)
            })

    member _.TryFinally
        (
            flow: AsyncFlow<'env, 'error, 'value>,
            compensation: unit -> unit
        ) : AsyncFlow<'env, 'error, 'value> =
        AsyncFlow(fun environment ->
            async {
                try
                    return! AsyncFlow.run environment flow
                finally
                    compensation ()
            })

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> AsyncFlow<'env, 'error, 'value>
        ) : AsyncFlow<'env, 'error, 'value>
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
            body: AsyncFlow<'env, 'error, unit>
        ) : AsyncFlow<'env, 'error, unit> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> AsyncFlow<'env, 'error, unit>
        ) : AsyncFlow<'env, 'error, unit> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

[<AutoOpen>]
module Builders =
    /// <summary>
    /// The fail-fast <c>result { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when the happy path should short-circuit on the first error
    /// and you want to keep the workflow in <c>Result</c> shape all the way through.
    /// </para>
    /// <para>
    /// It works well for parsing, validation, and other boundaries where failure is expected
    /// to stop the flow immediately instead of accumulating diagnostics.
    /// </para>
    /// <para>
    /// Use <c>Check.orError</c> when a pure check needs a domain error, and <c>Guard.MapError</c> when
    /// you need to remap an existing error before entering the CE.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let parsedUser =
    ///     result {
    ///         let! age = parseAge input
    ///         let! name = parseName input
    ///         return { Age = age; Name = name }
    ///     }
    /// ```
    /// </example>
    let result = ResultBuilder()

    /// <summary>
    /// The sync-only <c>flow { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when the boundary is synchronous and you want explicit environment
    /// reads without introducing async or task scheduling.
    /// </para>
    /// <para>
    /// It is the simplest builder in the library and is a good default for pure composition
    /// and deterministic orchestration.
    /// </para>
    /// <para>
    /// Use <c>Guard.Of</c> for check-like sources such as <c>option</c>, <c>voption</c>,
    /// <c>bool</c>, and <c>Result&lt;_, unit&gt;</c>. The CE then binds the resulting
    /// source value directly while the supplied error stays attached to the failure path.
    /// </para>
    /// <para>
    /// Use <c>Guard.MapError</c> when the source already carries an error and you want to keep the
    /// same source shape while changing the error type.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let greeting =
    ///     flow {
    ///         let! name = Flow.read (fun env -> env.Name)
    ///         return $"Hello, {name}"
    ///     }
    /// ```
    /// </example>
    let flow = FlowBuilder()

    /// <summary>
    /// The core <c>asyncFlow { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when the runtime boundary is async-first and you need to compose
    /// <c>Async</c> work with the same explicit environment model as <c>Flow</c>.
    /// </para>
    /// <para>
    /// It is the right landing point for async orchestration that still wants typed failures
    /// instead of exceptions.
    /// </para>
    /// <para>
    /// Use <c>Guard.Of</c> for check-like sources and <c>Guard.MapError</c> for
    /// existing-error remapping before binding into the async CE. `Guard` keeps the source
    /// visible to the CE and only packages the failure value.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let fetchProfile =
    ///     asyncFlow {
    ///         let! api = AsyncFlow.read (fun env -> env.Api)
    ///         let! profile = api.LoadProfile()
    ///         return profile
    ///     }
    /// ```
    /// </example>
    let asyncFlow = AsyncFlowBuilder()

    /// <summary>
    /// The accumulating <c>validate { }</c> computation expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this builder when you want to collect all validation failures instead of stopping
    /// at the first one.
    /// </para>
    /// <para>
    /// Use <c>and!</c> when sibling validations should accumulate into the same diagnostics graph.
    /// Plain <c>let!</c> and <c>do!</c> are sequential: if the left side fails, the later step is
    /// not evaluated.
    /// </para>
    /// <para>
    /// `Check<'value>` covers both value-preserving checks and gate checks.
    /// Use <c>Check.orError</c> to attach an application error, and <c>Guard.Of</c> /
    /// <c>Guard.MapError</c> when you want the same error-bound source shape to participate
    /// directly in validation.
    /// </para>
    /// <para>
    /// When nested API response fields need to keep their place in the diagnostics graph, use
    /// the scoped helpers <c>validate.key</c>, <c>validate.index</c>, and <c>validate.name</c>
    /// inside the computation expression. If you already have a <c>Validation</c> value, use
    /// <c>Validation.key</c>, <c>Validation.index</c>, or <c>Validation.name</c> to prefix it
    /// after the fact.
    /// </para>
    /// <para>
    /// It is intended for forms, configuration checks, and other input-heavy boundaries where
    /// the user benefits from seeing every problem at once.
    /// </para>
    /// </remarks>
    /// <example>
    /// ```fsharp
    /// let validatedUser =
    ///     validate {
    ///         let! name = Check.notBlank input.Name
    ///         let! age = Check.okIf (input.Age > 0) "Age must be positive"
    ///         return { Name = name; Age = age }
    ///     }
    /// ```
    ///
    /// ```fsharp
    /// let validatedCustomer =
    ///     validate.key "customer" {
    ///         let! name =
    ///             validate.name "Name" {
    ///                 return! input.Name |> Check.notBlank |> Check.orError "Name required"
    ///             }
    ///
    ///         return name
    ///     }
    /// ```
    /// </example>
    let validate = ValidateBuilder()
