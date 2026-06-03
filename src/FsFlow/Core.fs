namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks

#if FABLE_COMPILER
open Fable.Core
#endif

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
    /// <summary>Two causes happened sequentially; the left cause happened before the right cause.</summary>
    | Then of Cause<'error> * Cause<'error>
    /// <summary>Two causes happened concurrently; neither cause is ordered before the other.</summary>
    | Both of Cause<'error> * Cause<'error>
    /// <summary>A cause annotated with diagnostic trace text.</summary>
    | Traced of Cause<'error> * trace: string

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
    /// <summary>Transforms the error value of a failure cause using the provided function.</summary>
    /// <param name="mapper">The function to transform the error value.</param>
    /// <param name="cause">The original cause to transform.</param>
    /// <returns>A new cause with the transformed error value, or the original cause if it was not a <c>Fail</c>.</returns>
    let rec map (mapper: 'e -> 'f) (cause: Cause<'e>) : Cause<'f> =
        match cause with
        | Cause.Fail e -> Cause.Fail (mapper e)
        | Cause.Die ex -> Cause.Die ex
        | Cause.Interrupt -> Cause.Interrupt
        | Cause.Then(left, right) -> Cause.Then(map mapper left, map mapper right)
        | Cause.Both(left, right) -> Cause.Both(map mapper left, map mapper right)
        | Cause.Traced(inner, trace) -> Cause.Traced(map mapper inner, trace)

    /// <summary>Combines causes that happened sequentially.</summary>
    let thenCause (left: Cause<'error>) (right: Cause<'error>) : Cause<'error> =
        Cause.Then(left, right)

    /// <summary>Combines causes that happened concurrently.</summary>
    let both (left: Cause<'error>) (right: Cause<'error>) : Cause<'error> =
        Cause.Both(left, right)

    /// <summary>Attaches diagnostic trace text to a cause.</summary>
    let traced (trace: string) (cause: Cause<'error>) : Cause<'error> =
        Cause.Traced(cause, trace)

    /// <summary>Returns every typed failure value contained in a cause tree.</summary>
    let rec failures (cause: Cause<'error>) : 'error list =
        match cause with
        | Cause.Fail error -> [ error ]
        | Cause.Die _ -> []
        | Cause.Interrupt -> []
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> failures left @ failures right
        | Cause.Traced(inner, _) -> failures inner

    /// <summary>Returns every defect exception contained in a cause tree.</summary>
    let rec defects (cause: Cause<'error>) : exn list =
        match cause with
        | Cause.Fail _ -> []
        | Cause.Die error -> [ error ]
        | Cause.Interrupt -> []
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> defects left @ defects right
        | Cause.Traced(inner, _) -> defects inner

    /// <summary>Returns whether the cause tree contains an interruption signal.</summary>
    let rec isInterrupted (cause: Cause<'error>) : bool =
        match cause with
        | Cause.Interrupt -> true
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> isInterrupted left || isInterrupted right
        | Cause.Traced(inner, _) -> isInterrupted inner
        | Cause.Fail _
        | Cause.Die _ -> false

    /// <summary>Pretty prints a cause tree for diagnostics.</summary>
    let prettyPrint (formatError: 'error -> string) (cause: Cause<'error>) : string =
        let rec loop indent current =
            let padding = String.replicate indent " "

            match current with
            | Cause.Fail error -> $"{padding}Fail({formatError error})"
            | Cause.Die error ->
#if FABLE_COMPILER
                $"{padding}Die(Exception: {error.Message})"
#else
                $"{padding}Die({error.GetType().Name}: {error.Message})"
#endif
            | Cause.Interrupt -> $"{padding}Interrupt"
            | Cause.Then(left, right) ->
                $"{padding}Then\n{loop (indent + 2) left}\n{loop (indent + 2) right}"
            | Cause.Both(left, right) ->
                $"{padding}Both\n{loop (indent + 2) left}\n{loop (indent + 2) right}"
            | Cause.Traced(inner, trace) ->
                $"{padding}Traced({trace})\n{loop (indent + 2) inner}"

        loop 0 cause

[<RequireQualifiedAccess>]
module Exit =
    /// <summary>Transforms the success value of an exit outcome using the provided function.</summary>
    /// <param name="mapper">The function to transform the success value.</param>
    /// <param name="exit">The exit outcome to transform.</param>
    /// <returns>A new exit outcome with the transformed success value.</returns>
    let map (mapper: 'v -> 'w) (exit: Exit<'v, 'e>) : Exit<'w, 'e> =
        match exit with
        | Exit.Success v -> Exit.Success (mapper v)
        | Exit.Failure c -> Exit.Failure c

    /// <summary>Binds the success value of an exit outcome to a function that returns a new exit outcome.</summary>
    /// <param name="binder">The function that takes a success value and returns a new exit outcome.</param>
    /// <param name="exit">The exit outcome to bind.</param>
    /// <returns>The result of the binder function if the exit was successful; otherwise, the original failure.</returns>
    let bind (binder: 'v -> Exit<'w, 'e>) (exit: Exit<'v, 'e>) : Exit<'w, 'e> =
        match exit with
        | Exit.Success v -> binder v
        | Exit.Failure c -> Exit.Failure c

    /// <summary>Transforms the error value of a failed exit outcome using the provided function.</summary>
    /// <param name="mapper">The function to transform the error value.</param>
    /// <param name="exit">The exit outcome to transform.</param>
    /// <returns>A new exit outcome with the transformed error value.</returns>
    let mapError (mapper: 'e -> 'f) (exit: Exit<'v, 'e>) : Exit<'v, 'f> =
        match exit with
        | Exit.Success v -> Exit.Success v
        | Exit.Failure c -> Exit.Failure (Cause.map mapper c)

    /// <summary>Transforms both success and failure outcomes of an exit using the provided functions.</summary>
    /// <param name="onSuccess">The function to transform the success value.</param>
    /// <param name="onFailure">The function to transform the failure cause.</param>
    /// <param name="exit">The exit outcome to transform.</param>
    /// <returns>A new exit outcome with transformed values.</returns>
    let mapBoth (onSuccess: 'v -> 'w) (onFailure: Cause<'e> -> Cause<'f>) (exit: Exit<'v, 'e>) : Exit<'w, 'f> =
        match exit with
        | Exit.Success v -> Exit.Success (onSuccess v)
        | Exit.Failure c -> Exit.Failure (onFailure c)

    /// <summary>Creates an exit outcome from a standard F# <c>Result</c>.</summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>An exit outcome representing the result.</returns>
    let fromResult (result: Result<'v, 'e>) : Exit<'v, 'e> =
        match result with
        | Ok v -> Exit.Success v
        | Error e -> Exit.Failure (Cause.Fail e)

    /// <summary>Converts an exit outcome to a standard F# <c>Result</c>.</summary>
    /// <param name="exit">The exit outcome to convert.</param>
    /// <returns>A <c>Result</c> representing the successful value or the domain failure.</returns>
    /// <exception cref="T:System.Exception">Re-throws the original exception if the exit was <c>Cause.Die</c>.</exception>
    /// <exception cref="T:System.OperationCanceledException">Throws if the exit was <c>Cause.Interrupt</c>.</exception>
    let toResult (exit: Exit<'v, 'e>) : Result<'v, 'e> =
        match exit with
        | Exit.Success v -> Ok v
        | Exit.Failure (Cause.Fail e) -> Error e
        | Exit.Failure (Cause.Die ex) -> raise ex
        | Exit.Failure Cause.Interrupt -> raise (OperationCanceledException("Workflow was interrupted"))
        | Exit.Failure cause ->
            let defects = Cause.defects cause

            if not (List.isEmpty defects) then
                raise (AggregateException("Workflow failed with one or more defects.", defects))
            elif Cause.isInterrupted cause then
                raise (OperationCanceledException("Workflow was interrupted"))
            else
                let rendered = Cause.prettyPrint (fun error -> string error) cause
                raise (InvalidOperationException($"Workflow failed with a composite cause that cannot be represented as Result: {rendered}"))

/// <summary>Unique identifier for a running fiber.</summary>
[<Struct>]
type FiberId =
    | FiberId of int64

    /// <summary>The numeric fiber identifier.</summary>
    member this.Value =
        let (FiberId value) = this
        value

/// <summary>Describes the current lifecycle state of a fiber.</summary>
[<RequireQualifiedAccess>]
type FiberStatus =
    /// <summary>The fiber is currently running.</summary>
    | Running
    /// <summary>The fiber completed with a successful value.</summary>
    | Succeeded
    /// <summary>The fiber completed with a typed failure or defect.</summary>
    | Failed
    /// <summary>The fiber completed with an interruption cause.</summary>
    | Interrupted

/// <summary>Diagnostic metadata for a running fiber.</summary>
type FiberMetadata =
    {
        /// <summary>The unique fiber id.</summary>
        Id: FiberId
        /// <summary>The parent fiber id, if the fiber was forked from another fiber.</summary>
        ParentId: FiberId option
        /// <summary>The UTC timestamp when the fiber started.</summary>
        StartedAt: DateTimeOffset
        /// <summary>The current fiber status.</summary>
        mutable Status: FiberStatus
    }

/// <summary>Human-readable diagnostic dump for a fiber.</summary>
type FiberDump =
    {
        /// <summary>The fiber id.</summary>
        Id: FiberId
        /// <summary>The parent fiber id, if available.</summary>
        ParentId: FiberId option
        /// <summary>The UTC timestamp when the fiber started.</summary>
        StartedAt: DateTimeOffset
        /// <summary>The current fiber status.</summary>
        Status: FiberStatus
    }

/// <summary>
/// Represents a handle to a workflow that has already been started.
/// </summary>
/// <remarks>
/// A fiber is the hot counterpart to a cold <c>Flow</c>. It keeps the running
/// work's typed failure and success channels available through <c>Flow.join</c>,
/// and it carries an interruption source so parent workflows can ask the child
/// to stop and then wait for cleanup to finish.
/// </remarks>
/// <typeparam name="error">The failure type of the running workflow.</typeparam>
/// <typeparam name="value">The success type of the running workflow.</typeparam>
type Fiber<'error, 'value> =
    {
        /// <summary>Diagnostic metadata for the running fiber.</summary>
        Metadata: FiberMetadata
        /// <summary>The asynchronous operation that completes with the workflow's final exit outcome.</summary>
#if FABLE_COMPILER
        ExitTask: Async<Exit<'value, 'error>>
#else
        ExitTask: Task<Exit<'value, 'error>>
#endif
        /// <summary>The cancellation source used by <c>Flow.interrupt</c> to signal interruption.</summary>
        InterruptSource: CancellationTokenSource
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Fiber =
    /// <summary>Returns a snapshot of the current fiber metadata.</summary>
    let dump (fiber: Fiber<'error, 'value>) : FiberDump =
        {
            Id = fiber.Metadata.Id
            ParentId = fiber.Metadata.ParentId
            StartedAt = fiber.Metadata.StartedAt
            Status = fiber.Metadata.Status
        }

#if !FABLE_COMPILER
/// <summary>
/// Represents delayed task work that can observe a runtime cancellation token when it is started.
/// </summary>
/// <typeparam name="value">The type of the produced task value.</typeparam>
type ColdTask<'value> =
    | ColdTask of (CancellationToken -> Task<'value>)

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
#endif

/// <summary>
/// Represents the portable execution shape used by the unified <see cref="T:FsFlow.Flow`3" />.
/// </summary>
#if FABLE_COMPILER
type Effect<'value, 'error> = Async<Exit<'value, 'error>>
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
    | Flow of ('env -> CancellationToken -> Effect<'value, 'error>)

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

/// <summary>
/// Represents an error channel that cannot occur.
/// </summary>
type Never = private Never of unit

/// <summary>Nominal contract for an explicit service dependency.</summary>
/// <remarks>
/// Environments implement <c>IHas&lt;'service&gt;</c> when they can supply one service value of that type
/// through <see cref="T:FsFlow.Service`1" />.
/// </remarks>
/// <typeparam name="service">The dependency type exposed by the environment.</typeparam>
/// <example>
/// <code>
/// type IDb =
///     abstract Query : string -> string
///
/// type AppEnv =
///     { Database : IDb }
///     interface IHas&lt;IDb&gt; with member x.Service = x.Database
///
/// let db = Service&lt;IDb&gt;.get&lt;AppEnv, unit&gt;()
/// </code>
/// </example>
type IHas<'service> =
    abstract Service : 'service

/// <summary>Typed accessors for explicit and provider-resolved services.</summary>
/// <typeparam name="service">The service type being requested.</typeparam>
type Service<'service> private () =
    static member inline private succeed<'error> (value: 'service) : Effect<'service, 'error> =
        #if FABLE_COMPILER
        async.Return(Exit.Success value)
        #else
        ValueTask<Exit<'service, 'error>>(Exit.Success value)
        #endif

    /// <summary>Reads a statically declared service from an environment that implements <c>IHas&lt;'service&gt;</c>.</summary>
    /// <typeparam name="env">The environment type.</typeparam>
    /// <typeparam name="error">The workflow error type.</typeparam>
    /// <returns>A flow that succeeds with the requested service instance.</returns>
    static member inline get<'env, 'error when 'env :> IHas<'service>> () : Flow<'env, 'error, 'service> =
        Flow(fun environment _ ->
            let env = environment :> IHas<'service>
            Service<'service>.succeed env.Service)

    /// <summary>Resolves a service dynamically from an <see cref="T:System.IServiceProvider" /> environment.</summary>
    /// <typeparam name="env">The environment type.</typeparam>
    /// <typeparam name="error">The workflow error type.</typeparam>
    /// <returns>A flow that succeeds with the requested service instance.</returns>
    /// <remarks>
    /// Missing registrations are treated as configuration defects and therefore fail through
    /// <c>Cause.Die</c> rather than the typed error channel.
    /// </remarks>
    static member inline resolve<'env, 'error when 'env :> IServiceProvider> () : Flow<'env, 'error, 'service> =
        Flow(fun environment _ ->
            let env = environment :> IServiceProvider
            let service = env.GetService(typeof<'service>)

            if isNull (box service) then
                failwith $"Service {typeof<'service>.Name} was not registered in the IServiceProvider."
            else
                Service<'service>.succeed (unbox<'service> service))
