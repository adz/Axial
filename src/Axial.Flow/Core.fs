namespace Axial.Flow

open System
open System.ComponentModel
open System.Threading
open System.Threading.Tasks

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
                $"{padding}Die({Platform.dieDescription error})"
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
        /// <summary>
        /// Whether the fiber's outcome was consumed (<c>Flow.join</c>, <c>Flow.interrupt</c>) or explicitly
        /// detached at birth (<c>Flow.forkDetached</c>). A fiber that dies with a defect while unobserved is
        /// reported through the runtime's fiber observer once no observation can happen anymore.
        /// </summary>
        mutable Observed: bool
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
/// Runtime hooks observing fiber lifecycle events for diagnostics and telemetry.
/// </summary>
/// <remarks>
/// Installed once at the application edge with <c>Flow.withFiberObserver</c> and carried implicitly to every
/// descendant fork. All hooks default to no-ops, receive only diagnostic data (<c>FiberMetadata</c> and defect
/// exceptions, never typed exits), and must not throw; exceptions raised by hooks are swallowed so a
/// diagnostics hook can never alter a fiber's outcome.
/// </remarks>
type FiberObserver =
    {
        /// <summary>A fiber was forked. Receives the child fiber's metadata.</summary>
        OnStart: FiberMetadata -> unit
        /// <summary>
        /// A fiber settled. <c>FiberMetadata.Status</c> distinguishes success, failure, and interruption; the
        /// first <c>Cause.Die</c> defect in the exit, if any, is passed alongside.
        /// </summary>
        OnEnd: FiberMetadata -> exn option -> unit
        /// <summary>
        /// A <c>Cause.Die</c> defect became unobservable: a forked fiber died unobserved and no observation can
        /// happen anymore, or the runtime discarded a race/timeout loser's exit. The metadata is absent for
        /// discarded race/timeout losers, which are executions rather than fibers.
        /// </summary>
        OnUnobservedDefect: FiberMetadata option -> exn -> unit
    }

/// <summary>Standard fiber observers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FiberObserver =
    /// <summary>The default observer: every hook is a no-op.</summary>
    let none : FiberObserver =
        {
            OnStart = ignore
            OnEnd = fun _ _ -> ()
            OnUnobservedDefect = fun _ _ -> ()
        }

    let internal notifyStart (observer: FiberObserver) (metadata: FiberMetadata) : unit =
        try observer.OnStart metadata with _ -> ()

    let internal notifyEnd (observer: FiberObserver) (metadata: FiberMetadata) (defect: exn option) : unit =
        try observer.OnEnd metadata defect with _ -> ()

    let internal notifyUnobservedDefect
        (observer: FiberObserver)
        (metadata: FiberMetadata option)
        (defect: exn)
        : unit =
        try observer.OnUnobservedDefect metadata defect with _ -> ()

/// <summary>
/// Tracks a forked fiber's settled defect so it can be reported as unobserved exactly once, by whichever
/// detection mechanism (scope-close sweep or garbage-collection net) reaches finality first.
/// </summary>
type internal FiberDefectTracker(metadata: FiberMetadata, observer: FiberObserver) =
    let gate = obj()
    let mutable defect: exn option = None
    let mutable reported = false

#if !FABLE_COMPILER
    static let sentinels = System.Runtime.CompilerServices.ConditionalWeakTable<obj, FiberDefectTracker>()
#endif

    /// Records the defect the fiber settled with, if any.
    member _.Settled(settledDefect: exn option) =
        lock gate (fun () -> defect <- settledDefect)

    /// Reports the fiber's defect as unobserved if it has one, was never observed, and was not already
    /// reported. Safe to call from the scope sweep and the GC net concurrently.
    member _.TryReport() =
        let toReport =
            lock gate (fun () ->
                if not reported && not metadata.Observed then
                    match defect with
                    | Some _ ->
                        reported <- true
                        defect
                    | None -> None
                else
                    None)

        match toReport with
        | Some exn -> FiberObserver.notifyUnobservedDefect observer (Some metadata) exn
        | None -> ()

#if !FABLE_COMPILER
    /// Keeps <paramref name="tracker" /> alive exactly as long as <paramref name="fiberHandle" /> is
    /// reachable. When a discarded handle is collected, the tracker becomes collectable and its finalizer
    /// reports any unobserved defect — the same mechanism as <c>TaskScheduler.UnobservedTaskException</c>.
    static member Attach(fiberHandle: obj, tracker: FiberDefectTracker) =
        sentinels.Add(fiberHandle, tracker)

    override this.Finalize() =
        try this.TryReport() with _ -> ()
#endif

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
        ExitTask: Platform.ExitTask<'value, 'error>
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

type internal Execution<'value, 'error> = Platform.Execution<'value, 'error>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal FiberId =
    let private nextValue = ref 0L

    let next () =
        FiberId(Platform.nextId nextValue)

/// <summary>
/// Owns finalizers for resources acquired during provisioning or runtime execution.
/// </summary>
/// <remarks>
/// Scopes aggregate cleanup in reverse registration order, prevent double-disposal, and surface
/// cleanup failures as defects rather than typed business errors.
/// </remarks>
type Scope() =
    let gate = obj()
    let finalizers = ResizeArray<Platform.Finalizer>()
    let mutable closed = false

    member _.AddFinalizer(finalizer: Platform.Finalizer) =
        if isNull (box finalizer) then
            nullArg (nameof finalizer)

        lock gate (fun () ->
            if closed then
                raise (ObjectDisposedException(nameof Scope))
            else
                finalizers.Add finalizer)

    member this.AddDisposable(resource: IDisposable) =
        if isNull (box resource) then
            nullArg (nameof resource)

        this.AddFinalizer(fun _ ->
            resource.Dispose()
            Platform.completedDeed ())

    member this.AddAsyncDisposable(resource: IAsyncDisposable) =
        if isNull (box resource) then
            nullArg (nameof resource)

        this.AddFinalizer(fun _ -> Platform.disposeAsyncDeed resource)

    member this.AddChild() =
        let child = new Scope()

        this.AddFinalizer(fun cancellationToken -> child.Close(cancellationToken))

        child

    member _.Close(cancellationToken: CancellationToken) : Platform.Deed =
        let snapshot =
            lock gate (fun () ->
                if closed then
                    [||]
                else
                    closed <- true
                    finalizers.ToArray())

        Platform.runFinalizers snapshot cancellationToken

#if !FABLE_COMPILER
    interface IAsyncDisposable with
        member this.DisposeAsync() =
            ValueTask(this.Close(CancellationToken.None))

    interface IDisposable with
        member this.Dispose() =
            this.Close(CancellationToken.None).GetAwaiter().GetResult()
#endif

type internal RuntimeContext =
    {
        Scope: Scope
        Annotations: Map<string, string>
        AnnotationSink: string -> string -> unit
        FiberId: FiberId
        Observer: FiberObserver
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeContext =
    let create (scope: Scope) : RuntimeContext =
        {
            Scope = scope
            Annotations = Map.empty
            AnnotationSink = fun _ _ -> ()
            FiberId = FiberId.next ()
            Observer = FiberObserver.none
        }

    let detached : RuntimeContext =
        create (new Scope())

    let withScope (scope: Scope) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Scope = scope }

    let withAnnotation (name: string) (value: string) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Annotations = runtime.Annotations |> Map.add name value }

    let withAnnotationSink (sink: string -> string -> unit) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with AnnotationSink = sink }

    let withFiberId (fiberId: FiberId) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with FiberId = fiberId }

    let withObserver (observer: FiberObserver) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Observer = observer }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeState =
    let private currentRuntime = Platform.newCell RuntimeContext.detached

    let current () : RuntimeContext =
        currentRuntime |> Platform.getCellOrDefault (fun () -> RuntimeContext.detached)

    let withRuntime (runtime: RuntimeContext) (operation: unit -> 'value) : 'value =
        Platform.withCell currentRuntime runtime operation

/// <summary>
/// Represents a cold workflow that reads an environment, returns a typed result, and is executed
/// explicitly through one of its execution members such as <c>ToTask</c>, <c>ToAsync</c>, or <c>RunSynchronously</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type Flow<'env, 'error, 'value> =
    internal
    | Flow of ('env -> CancellationToken -> Execution<'value, 'error>)

    member private this.ToExecution(environment: 'env, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        let (Flow operation) = this
        let scope = new Scope()
        let runtime = RuntimeContext.create scope

        Platform.runScoped
            scope.Close
            cancellationToken
            (fun () -> RuntimeState.withRuntime runtime (fun () -> operation environment cancellationToken))
            (fun cleanupError executionError exit ->
                let causeOf (error: exn) : Cause<'error> =
                    if error :? OperationCanceledException then Cause.Interrupt else Cause.Die error

                let primary =
                    match executionError, exit with
                    | Some error, _ -> Exit.Failure (causeOf error)
                    | None, Some result -> result
                    | None, None -> Exit.Failure (Cause.Die (InvalidOperationException "Flow execution produced no outcome."))

                match cleanupError, primary with
                | Some error, Exit.Failure cause ->
                    Exit.Failure (Cause.thenCause cause (causeOf error))
                | Some error, Exit.Success _ ->
                    Exit.Failure (causeOf error)
                | None, result ->
                    result)

    /// <summary>Starts the workflow and returns an F# async handle that completes with the final exit.</summary>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token to use instead of <c>Async.CancellationToken</c>.</param>
    /// <returns>An async handle that completes with the workflow exit.</returns>
    /// <platforms>Fable compatible</platforms>
    member this.ToAsync(environment: 'env, ?cancellationToken: CancellationToken) : Async<Exit<'value, 'error>> =
        async {
            let! token =
                match cancellationToken with
                | Some token -> async.Return token
                | None -> Async.CancellationToken

            return! Platform.executionToAsync (this.ToExecution(environment, token))
        }

#if !FABLE_COMPILER
    /// <summary>Starts the workflow and returns a value-task handle that completes with the final exit.</summary>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to <see cref="F:System.Threading.CancellationToken.None" />.</param>
    /// <returns>A value task that completes with the workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.ToValueTask(environment: 'env, ?cancellationToken: CancellationToken) : ValueTask<Exit<'value, 'error>> =
        this.ToExecution(environment, defaultArg cancellationToken CancellationToken.None)

    /// <summary>Starts the workflow and returns a task handle that completes with the final exit.</summary>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to <see cref="F:System.Threading.CancellationToken.None" />.</param>
    /// <returns>A task that completes with the workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.ToTask(environment: 'env, ?cancellationToken: CancellationToken) : Task<Exit<'value, 'error>> =
        this.ToExecution(environment, defaultArg cancellationToken CancellationToken.None).AsTask()

    /// <summary>Starts the workflow and blocks until the final exit is available.</summary>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="timeout">The optional timeout in milliseconds.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to <see cref="F:System.Threading.CancellationToken.None" />.</param>
    /// <returns>The final workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.RunSynchronously
        (
            environment: 'env,
            ?timeout: int,
            ?cancellationToken: CancellationToken
        ) : Exit<'value, 'error> =
        let task = this.ToTask(environment, cancellationToken = defaultArg cancellationToken CancellationToken.None)

        match timeout with
        | None ->
            task.GetAwaiter().GetResult()
        | Some millisecondsTimeout ->
            if task.Wait(millisecondsTimeout) then
                task.GetAwaiter().GetResult()
            else
                raise (TimeoutException("The flow did not complete before the timeout."))
#endif

[<EditorBrowsable(EditorBrowsableState.Never)>]
module internal FlowInternal =
    let invoke
        (Flow operation: Flow<'env, 'error, 'value>)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : Execution<'value, 'error> =
        operation environment cancellationToken

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
/// Defines how <c>Flow.Runtime.supervise</c> restarts flows that terminate with unexpected defects.
/// </summary>
/// <remarks>
/// The defect-channel sibling of <see cref="T:Axial.Flow.RetryPolicy`1" />: it decides restarts from the
/// defect exception rather than the typed error, because defects are bugs that escaped the typed channel.
/// </remarks>
type SupervisePolicy =
    {
      MaxAttempts: int
      Delay: int -> TimeSpan
      ShouldRestart: exn -> bool
    }

/// <summary>
/// Standard supervision policies for runtime helpers.
/// </summary>
[<RequireQualifiedAccess>]
module SupervisePolicy =
    let noDelay (maxAttempts: int) : SupervisePolicy =
        { MaxAttempts = maxAttempts
          Delay = fun _ -> TimeSpan.Zero
          ShouldRestart = fun _ -> true }

/// <summary>
/// Represents an error channel that cannot occur.
/// </summary>
type Never = private Never of unit

/// <summary>A flow that requires no environment and cannot fail with a typed error.</summary>
type Flow<'value> = Flow<unit, Never, 'value>

/// <summary>A flow that requires no environment and can fail with a typed error.</summary>
type Flow<'error, 'value> = Flow<unit, 'error, 'value>

/// <summary>A flow that reads an environment and cannot fail with a typed error.</summary>
type EnvFlow<'env, 'value> = Flow<'env, Never, 'value>

/// <summary>A flow that requires no environment and uses exceptions as recoverable typed errors.</summary>
type ExnFlow<'value> = Flow<unit, exn, 'value>

/// <summary>A flow that reads an environment and uses exceptions as recoverable typed errors.</summary>
type ExnEnvFlow<'env, 'value> = Flow<'env, exn, 'value>

/// <summary>Nominal contract for an explicit service dependency.</summary>
/// <remarks>
/// Environments implement <c>IHas&lt;'service&gt;</c> when they can supply one service value of that type
/// through <see cref="T:Axial.Service`1" />.
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
    static member private succeed<'error> (value: 'service) : Execution<'service, 'error> =
        Platform.ofExit (Exit.Success value)

    /// <summary>Reads a statically declared service from an environment that implements <c>IHas&lt;'service&gt;</c>.</summary>
    /// <typeparam name="env">The environment type.</typeparam>
    /// <typeparam name="error">The workflow error type.</typeparam>
    /// <returns>A flow that succeeds with the requested service instance.</returns>
    static member get<'env, 'error when 'env :> IHas<'service>> () : Flow<'env, 'error, 'service> =
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
    static member resolve<'env, 'error when 'env :> IServiceProvider> () : Flow<'env, 'error, 'service> =
        Flow(fun environment _ ->
            match Platform.resolveService<'service> (environment :> IServiceProvider) with
            | Some service -> Service<'service>.succeed service
            | None -> Platform.serviceResolutionUnavailable<'service, 'error> ())
