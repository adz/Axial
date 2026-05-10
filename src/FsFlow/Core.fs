namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents the portable execution shape used by the unified <see cref="T:FsFlow.Flow`3" />.
/// </summary>
#if FABLE_COMPILER
type Effect<'value, 'error> = JS.Promise<Result<'value, 'error>>
#else
type Effect<'value, 'error> = ValueTask<Result<'value, 'error>>
#endif

/// <summary>
/// Represents a cold workflow that reads an environment, returns a typed result, and is executed
/// explicitly through <c>Flow.run</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type Flow<'env, 'error, 'value> =
    private
    | Flow of ('env -> CancellationToken -> Effect<'value, 'error>)

/// <summary>
/// Represents a cold async workflow that reads an environment, returns a typed result,
/// and is used internally to implement the unified <c>Flow</c> surface.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type internal AsyncFlow<'env, 'error, 'value> =
    private
    | AsyncFlow of ('env -> Async<Result<'value, 'error>>)

/// <summary>
/// Log levels used by runtime logging helpers and environment-provided logging functions.
/// </summary>
[<RequireQualifiedAccess>]
type LogLevel =
    | Trace
    | Debug
    | Information
    | Warning
    | Error
    | Critical

/// <summary>
/// A structured log entry written through a runtime logger.
/// </summary>
type LogEntry =
    {
      Level: LogLevel
      Message: string
      TimestampUtc: DateTimeOffset
    }

/// <summary>
/// Defines how runtime retry helpers repeat typed failures in a controlled way.
/// </summary>
type RetryPolicy<'error> =
    {
      MaxAttempts: int
      Delay: int -> TimeSpan
      ShouldRetry: 'error -> bool
    }

/// <summary>
/// Standard retry policies for runtime helpers.
/// </summary>
[<RequireQualifiedAccess>]
module RetryPolicy =
    let noDelay (maxAttempts: int) : RetryPolicy<'error> =
        { MaxAttempts = maxAttempts
          Delay = fun _ -> TimeSpan.Zero
          ShouldRetry = fun _ -> true }

/// <summary>Describes the capability contract for a single dependency.</summary>
/// <remarks>
/// Named cap-set interfaces inherit this contract once and then expose the dependency through a
/// member such as <c>Clock</c> or <c>Logger</c>. Workflow builders can accept any environment
/// that implements <c>Needs&lt;'dep&gt;</c>, which lets larger runtimes satisfy smaller
/// boundaries.
/// </remarks>
/// <typeparam name="dep">The dependency type exposed by the environment.</typeparam>
/// <example>
/// <code>
/// type IClock =
///     abstract UtcNow : unit -&gt; DateTimeOffset
///
/// type ClockCaps =
///     inherit Needs&lt;IClock&gt;
///     abstract Clock : IClock
/// </code>
/// </example>
type Needs<'dep> =
    abstract Dep : 'dep

/// <summary>Request token for binding a whole dependency inside a workflow.</summary>
/// <remarks>
/// Use this token when a workflow needs the dependency itself rather than a value projected from
/// that dependency. The <c>flow {}</c> builder and its internal compatibility helpers
/// read it from any environment that implements <c>Needs&lt;'dep&gt;</c>.
/// </remarks>
/// <typeparam name="dep">The dependency type to read from the environment.</typeparam>
/// <example>
/// <code>
/// type IClock =
///     abstract UtcNow : unit -&gt; DateTimeOffset
///
/// type ClockCaps =
///     inherit Needs&lt;IClock&gt;
///     abstract Clock : IClock
///
/// let readClock : Flow&lt;#ClockCaps, unit, IClock&gt; =
///     flow {
///         let! clock = Env&lt;IClock&gt;
///         return clock
///     }
/// </code>
/// </example>
[<Struct>]
type Env<'dep> =
    | Env

/// <summary>Request token for projecting a value from a dependency.</summary>
/// <remarks>
/// Builders read the dependency from the environment, apply the projection, and then reuse the
/// existing lift/bind behavior for the projected value. If the projection returns a
/// <c>Result</c>, <c>Async</c>, <c>Task</c>, <c>ValueTask</c>, <c>ColdTask</c>, <c>option</c>, or
/// <c>voption</c>, the existing workflow rules still apply.
/// </remarks>
/// <typeparam name="dep">The dependency type to read from the environment.</typeparam>
/// <typeparam name="value">The projected value type.</typeparam>
/// <example>
/// <code>
/// type IClock =
///     abstract UtcNow : unit -&gt; DateTimeOffset
///
/// type ClockCaps =
///     inherit Needs&lt;IClock&gt;
///     abstract Clock : IClock
///
/// let readClockNow : Flow&lt;#ClockCaps, unit, DateTimeOffset&gt; =
///     flow {
///         let! now = Env&lt;IClock&gt; _.UtcNow
///         return now
///     }
/// </code>
/// </example>
[<Struct>]
type Env<'dep, 'value> =
    | Env of ('dep -> 'value)

module private ResultFlow =
    let map
        (mapper: 'value -> 'next)
        (result: Result<'value, 'error>)
        : Result<'next, 'error> =
        Result.map mapper result

    let bind
        (binder: 'value -> Result<'next, 'error>)
        (result: Result<'value, 'error>)
        : Result<'next, 'error> =
        Result.bind binder result

    let mapError
        (mapper: 'error -> 'nextError)
        (result: Result<'value, 'error>)
        : Result<'value, 'nextError> =
        Result.mapError mapper result

module internal OptionFlow =
    let toUnitResult (value: 'value option) : Result<'value, unit> =
        match value with
        | Some innerValue -> Ok innerValue
        | None -> Error()

    let toUnitResultValueOption (value: 'value voption) : Result<'value, unit> =
        match value with
        | ValueSome innerValue -> Ok innerValue
        | ValueNone -> Error()

    let toResult (error: 'error) (value: 'value option) : Result<'value, 'error> =
        match value with
        | Some innerValue -> Ok innerValue
        | None -> Error error

    let toResultValueOption (error: 'error) (value: 'value voption) : Result<'value, 'error> =
        match value with
        | ValueSome innerValue -> Ok innerValue
        | ValueNone -> Error error

module internal EffectFlow =
    let ofResult (result: Result<'value, 'error>) : Effect<'value, 'error> =
#if FABLE_COMPILER
        JS.Constructors.Promise.resolve result
#else
        ValueTask<Result<'value, 'error>>(result)
#endif

    let ofValue (value: 'value) : Effect<'value, 'error> =
        ofResult (Ok value)

    let ofError (error: 'error) : Effect<'value, 'error> =
        ofResult (Error error)

    let fold
        (onSuccess: 'value -> Effect<'next, 'nextError>)
        (onError: 'error -> Effect<'next, 'nextError>)
        (effect: Effect<'value, 'error>)
        : Effect<'next, 'nextError> =
#if FABLE_COMPILER
        Promise.bind
            (function
             | Ok value -> onSuccess value
             | Error error -> onError error)
            effect
#else
        ValueTask<Result<'next, 'nextError>>(
            task {
                let! result = effect

                match result with
                | Ok value -> return! onSuccess value
                | Error error -> return! onError error
            })
#endif

    let map
        (mapper: 'value -> 'next)
        (effect: Effect<'value, 'error>)
        : Effect<'next, 'error> =
        fold (mapper >> ofValue) ofError effect

    let bind
        (binder: 'value -> Effect<'next, 'error>)
        (effect: Effect<'value, 'error>)
        : Effect<'next, 'error> =
        fold binder ofError effect

    let mapError
        (mapper: 'error -> 'nextError)
        (effect: Effect<'value, 'error>)
        : Effect<'value, 'nextError> =
        fold ofValue (mapper >> ofError) effect

module internal InternalCombinatorCore =
    let mapWith
        (mapOutcome: (Result<'value, 'error> -> Result<'next, 'error>) -> 'operation -> 'nextOperation)
        (mapper: 'value -> 'next)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> operation context |> mapOutcome (Result.map mapper)

    let bindWith
        (bindOutcome: 'operation -> ('value -> 'nextOperation) -> ('error -> 'nextOperation) -> 'nextOperation)
        (continueWith: 'context -> 'value -> 'nextOperation)
        (failWith: 'error -> 'nextOperation)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> bindOutcome (operation context) (continueWith context) failWith

    let mapErrorWith
        (mapOutcome: (Result<'value, 'error> -> Result<'value, 'nextError>) -> 'operation -> 'nextOperation)
        (mapper: 'error -> 'nextError)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> operation context |> mapOutcome (Result.mapError mapper)

    let localEnvWith
        (run: 'innerEnvironment -> 'flow -> 'operation)
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: 'flow)
        : 'outerEnvironment -> 'operation =
        fun environment -> flow |> run (mapping environment)

    let delayWith
        (run: 'environment -> 'flow -> 'operation)
        (factory: unit -> 'flow)
        : 'environment -> 'operation =
        fun environment -> factory () |> run environment

/// <summary>Describes a missing service-provider capability.</summary>
type MissingCapability =
    {
        CapabilityType: Type
    }

type Flow<'env, 'error, 'value> with
    static member CapabilityService
        (projection: 'env -> 'service)
        : Flow<'env, 'error, 'service> =
        Flow(fun environment _ -> EffectFlow.ofValue (projection environment))

    static member ServiceFromProvider
        ()
        : Flow<IServiceProvider, MissingCapability, 'service> =
        Flow(fun provider _ ->
            match provider.GetService typeof<'service> with
            | null ->
                EffectFlow.ofError
                    {
                        CapabilityType = typeof<'service>
                    }
            | value -> EffectFlow.ofValue (unbox<'service> value))

    static member ProvideLayer
        (
            layer: Flow<'input, 'error, 'environment>,
            flow: Flow<'environment, 'error, 'value>
        ) : Flow<'input, 'error, 'value> =
        let (Flow layerOperation) = layer
        let (Flow flowOperation) = flow

        Flow(fun environment ct ->
            match (layerOperation environment ct).GetAwaiter().GetResult() with
            | Ok environment -> flowOperation environment ct
            | Error error -> EffectFlow.ofError error)

type internal AsyncFlow<'env, 'error, 'value> with
    static member CapabilityService
        (projection: 'env -> 'service)
        : AsyncFlow<'env, 'error, 'service> =
        AsyncFlow(fun environment -> async.Return(Ok(projection environment)))

    static member ServiceFromProvider
        ()
        : AsyncFlow<IServiceProvider, MissingCapability, 'service> =
        AsyncFlow(fun provider ->
            async {
                match provider.GetService typeof<'service> with
                | null ->
                    return
                        Error
                            {
                                CapabilityType = typeof<'service>
                            }
                | value -> return Ok(unbox<'service> value)
            })

    static member ProvideLayer
        (
            layer: AsyncFlow<'input, 'error, 'environment>,
            flow: AsyncFlow<'environment, 'error, 'value>
        ) : AsyncFlow<'input, 'error, 'value> =
        let (AsyncFlow layerOperation) = layer
        let (AsyncFlow flowOperation) = flow

        AsyncFlow(fun environment ->
            async {
                let! outcome = layerOperation environment

                match outcome with
                | Ok environment -> return! flowOperation environment
                | Error error -> return Error error
            })
