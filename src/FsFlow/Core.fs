namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents the cause of a failed workflow.
/// </summary>
/// <typeparam name="error">The type of the domain-specific failure value.</typeparam>
[<RequireQualifiedAccess>]
type Cause<'error> =
    /// <summary>An expected domain-specific failure.</summary>
    | Fail of 'error
    /// <summary>An unexpected defect or panic (e.g., an exception).</summary>
    | Die of exn
    /// <summary>An administrative signal to stop the workflow (e.g., cancellation).</summary>
    | Interrupt

/// <summary>
/// Represents the final outcome of a workflow execution.
/// </summary>
/// <typeparam name="value">The type of the success value.</typeparam>
/// <typeparam name="error">The type of the domain-specific failure value.</typeparam>
[<RequireQualifiedAccess>]
type Exit<'value, 'error> =
    /// <summary>The workflow completed successfully.</summary>
    | Success of 'value
    /// <summary>The workflow failed due to a specific cause.</summary>
    | Failure of Cause<'error>

[<RequireQualifiedAccess>]
module Cause =
    let map (mapper: 'e -> 'f) (cause: Cause<'e>) : Cause<'f> =
        match cause with
        | Cause.Fail e -> Cause.Fail (mapper e)
        | Cause.Die ex -> Cause.Die ex
        | Cause.Interrupt -> Cause.Interrupt

[<RequireQualifiedAccess>]
module Exit =
    let map (mapper: 'v -> 'w) (exit: Exit<'v, 'e>) : Exit<'w, 'e> =
        match exit with
        | Exit.Success v -> Exit.Success (mapper v)
        | Exit.Failure c -> Exit.Failure c

    let bind (binder: 'v -> Exit<'w, 'e>) (exit: Exit<'v, 'e>) : Exit<'w, 'e> =
        match exit with
        | Exit.Success v -> binder v
        | Exit.Failure c -> Exit.Failure c

    let mapError (mapper: 'e -> 'f) (exit: Exit<'v, 'e>) : Exit<'v, 'f> =
        match exit with
        | Exit.Success v -> Exit.Success v
        | Exit.Failure c -> Exit.Failure (Cause.map mapper c)

    let fromResult (result: Result<'v, 'e>) : Exit<'v, 'e> =
        match result with
        | Ok v -> Exit.Success v
        | Error e -> Exit.Failure (Cause.Fail e)

    let toResult (exit: Exit<'v, 'e>) : Result<'v, 'e> =
        match exit with
        | Exit.Success v -> Ok v
        | Exit.Failure (Cause.Fail e) -> Error e
        | Exit.Failure (Cause.Die ex) -> raise ex
        | Exit.Failure Cause.Interrupt -> raise (OperationCanceledException("Workflow was interrupted"))

/// <summary>
/// Represents a handle to a running workflow.
/// </summary>
/// <typeparam name="error">The failure type of the running workflow.</typeparam>
/// <typeparam name="value">The success type of the running workflow.</typeparam>
type Fiber<'error, 'value> =
    {
        /// <summary>The task that completes when the workflow finishes execution.</summary>
        ExitTask: Task<Exit<'value, 'error>>
        /// <summary>The source used to signal interruption to the running workflow.</summary>
        InterruptSource: CancellationTokenSource
    }

/// <summary>
/// Represents the portable execution shape used by the unified <see cref="T:FsFlow.Flow`3" />.
/// </summary>
#if FABLE_COMPILER
type Effect<'value, 'error> = JS.Promise<Exit<'value, 'error>>
#else
type Effect<'value, 'error> = ValueTask<Exit<'value, 'error>>
#endif

/// <summary>
/// Represents a cold workflow that reads an environment, returns a typed result, and is executed
/// explicitly through <c>Flow.run</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type Flow<'env, 'error, 'value> =
    internal
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
    | AsyncFlow of ('env -> Async<Exit<'value, 'error>>)

/// <summary>
/// Represents a cold task-based workflow that reads an environment, observes a runtime cancellation token,
/// returns a typed result, and is executed explicitly through <c>TaskFlow.run</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type internal TaskFlow<'env, 'error, 'value> =
    private
    | TaskFlow of ('env -> CancellationToken -> Task<Exit<'value, 'error>>)

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
    let ofExit (exit: Exit<'value, 'error>) : Effect<'value, 'error> =
#if FABLE_COMPILER
        JS.Constructors.Promise.resolve exit
#else
        ValueTask<Exit<'value, 'error>>(exit)
#endif

    let ofValue (value: 'value) : Effect<'value, 'error> =
        ofExit (Exit.Success value)

    let ofCause (cause: Cause<'error>) : Effect<'value, 'error> =
        ofExit (Exit.Failure cause)

    let ofError (error: 'error) : Effect<'value, 'error> =
        ofCause (Cause.Fail error)

    let ofDie (exn: exn) : Effect<'value, 'error> =
        ofCause (Cause.Die exn)

    let ofInterrupt () : Effect<'value, 'error> =
        ofCause Cause.Interrupt

    let ofResult (result: Result<'value, 'error>) : Effect<'value, 'error> =
        match result with
        | Ok value -> ofValue value
        | Error error -> ofError error

    let fold
        (onSuccess: 'value -> Effect<'next, 'nextError>)
        (onFailure: Cause<'error> -> Effect<'next, 'nextError>)
        (effect: Effect<'value, 'error>)
        : Effect<'next, 'nextError> =
#if FABLE_COMPILER
        Promise.bind
            (function
             | Exit.Success value -> onSuccess value
             | Exit.Failure cause -> onFailure cause)
            effect
#else
        ValueTask<Exit<'next, 'nextError>>(
            task {
                let! exit = effect

                match exit with
                | Exit.Success value -> return! onSuccess value
                | Exit.Failure cause -> return! onFailure cause
            })
#endif

    let map
        (mapper: 'value -> 'next)
        (effect: Effect<'value, 'error>)
        : Effect<'next, 'error> =
        fold (mapper >> ofValue) ofCause effect

    let bind
        (binder: 'value -> Effect<'next, 'error>)
        (effect: Effect<'value, 'error>)
        : Effect<'next, 'error> =
        fold binder ofCause effect

    let mapError
        (mapper: 'error -> 'nextError)
        (effect: Effect<'value, 'error>)
        : Effect<'value, 'nextError> =
        fold ofValue (fun cause ->
            match cause with
            | Cause.Fail error -> ofError (mapper error)
            | Cause.Die exn -> ofDie exn
            | Cause.Interrupt -> ofInterrupt ()
        ) effect

module internal InternalCombinatorCore =
    let mapWith
        (mapOutcome: (Exit<'value, 'error> -> Exit<'next, 'error>) -> 'operation -> 'nextOperation)
        (mapper: 'value -> 'next)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> operation context |> mapOutcome (Exit.map mapper)

    let bindWith
        (bindOutcome: 'operation -> ('value -> 'nextOperation) -> (Cause<'error> -> 'nextOperation) -> 'nextOperation)
        (continueWith: 'context -> 'value -> 'nextOperation)
        (failWith: Cause<'error> -> 'nextOperation)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> bindOutcome (operation context) (continueWith context) failWith

    let mapErrorWith
        (mapOutcome: (Exit<'value, 'error> -> Exit<'value, 'nextError>) -> 'operation -> 'nextOperation)
        (mapper: 'error -> 'nextError)
        (operation: 'context -> 'operation)
        : 'context -> 'nextOperation =
        fun context -> operation context |> mapOutcome (Exit.mapError mapper)

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
            | Exit.Success environment -> flowOperation environment ct
            | Exit.Failure cause -> EffectFlow.ofCause cause)

type internal AsyncFlow<'env, 'error, 'value> with
    static member CapabilityService
        (projection: 'env -> 'service)
        : AsyncFlow<'env, 'error, 'service> =
        AsyncFlow(fun environment -> async.Return(Exit.Success(projection environment)))

    static member ServiceFromProvider
        ()
        : AsyncFlow<IServiceProvider, MissingCapability, 'service> =
        AsyncFlow(fun provider ->
            async {
                match provider.GetService typeof<'service> with
                | null ->
                    return
                        Exit.Failure (Cause.Fail
                            {
                                CapabilityType = typeof<'service>
                            })
                | value -> return Exit.Success(unbox<'service> value)
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
                | Exit.Success environment -> return! flowOperation environment
                | Exit.Failure cause -> return Exit.Failure cause
            })
