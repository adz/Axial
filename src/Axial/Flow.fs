namespace Axial

open System
open System.Threading
open System.Threading.Tasks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Resource =
#if !FABLE_COMPILER
    /// <summary>Describes acquisition together with a task-based release registered in the current Flow scope.</summary>
    let create
        (acquire: Flow<'env, 'error, 'value>)
        (release: 'value -> CancellationToken -> Task)
        : Resource<'env, 'error, 'value> =
        Resource(acquire, fun value scope -> scope.AddFinalizer(fun cancellationToken -> release value cancellationToken))

    /// <summary>Describes a task-based finalizer as a unit-valued resource.</summary>
    let finalizer (cleanup: CancellationToken -> Task) : Resource<'env, 'error, unit> =
        Resource(Flow(fun _ _ -> Execution.ofValue ()), fun () scope -> scope.AddFinalizer cleanup)
#endif

    /// <summary>Describes acquisition together with an F# async release registered in the current Flow scope.</summary>
    let ofAsync
        (acquire: Flow<'env, 'error, 'value>)
        (release: 'value -> CancellationToken -> Async<unit>)
        : Resource<'env, 'error, 'value> =
        Resource(
            acquire,
            fun value scope ->
                scope.AddFinalizer(fun cancellationToken ->
#if FABLE_COMPILER
                    release value cancellationToken
#else
                    Async.StartAsTask(release value cancellationToken) :> Task
#endif
                ))

    /// <summary>Describes an F# async finalizer as a unit-valued resource.</summary>
    let asyncFinalizer (cleanup: CancellationToken -> Async<unit>) : Resource<'env, 'error, unit> =
        ofAsync (Flow(fun _ _ -> Execution.ofValue ())) (fun () cancellationToken -> cleanup cancellationToken)

module Flow =
    let inline internal invoke
        (flow: Flow<'env, 'error, 'value>)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : Execution<'value, 'error> =
        FlowInternal.invoke flow environment cancellationToken

    let private combineCleanup
        (cleanupError: exn option)
        (executionError: exn option)
        (exit: Exit<'value, 'error> option)
        (missingOutcomeMessage: string)
        : Exit<'value, 'error> =
        let primary =
            match executionError, exit with
            | Some error, _ -> Exit.Failure (Execution.causeOfException error)
            | None, Some result -> result
            | None, None -> Exit.Failure (Cause.Die (InvalidOperationException missingOutcomeMessage))

        match cleanupError, primary with
        | Some error, Exit.Failure cause ->
            Exit.Failure (Cause.thenCause cause (Execution.causeOfException error))
        | Some error, Exit.Success _ ->
            Exit.Failure (Execution.causeOfException error)
        | None, result ->
            result

    let private chooseParallelExit
        (leftExit: Exit<'left, 'error>)
        (rightExit: Exit<'right, 'error>)
        : Exit<'left * 'right, 'error> =
        match leftExit, rightExit with
        | Exit.Success leftValue, Exit.Success rightValue ->
            Exit.Success(leftValue, rightValue)
        | Exit.Failure leftCause, Exit.Failure rightCause ->
            Exit.Failure(Cause.both leftCause rightCause)
        | Exit.Failure cause, Exit.Success _ ->
            Exit.Failure cause
        | Exit.Success _, Exit.Failure cause ->
            Exit.Failure cause

    /// Reports every defect inside a runtime-discarded exit (a race or timeout loser) as unobserved.
    /// Such exits are dropped without a fiber handle, so nobody can ever observe them.
    let private reportDiscardedExit (observer: FiberObserver) (exit: Exit<'value, 'error>) : unit =
        match exit with
        | Exit.Success _ -> ()
        | Exit.Failure cause ->
            for defect in Cause.defects cause do
                FiberObserver.notifyUnobservedDefect observer None defect

    let private runEffect
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        : Execution<'value, 'error> =
        let scope = new Scope()
        let runtime = RuntimeContext.create scope

        Platform.runScoped
            scope.Close
            cancellationToken
            (fun () -> RuntimeState.withRuntime runtime (fun () -> invoke flow environment cancellationToken))
            (fun cleanupError executionError exit ->
                combineCleanup cleanupError executionError exit "Flow execution produced no outcome.")

    /// <summary>Registers a F# async finalizer with the current runtime scope on .NET or Fable.</summary>
    /// <example><code>Flow.scopeAsyncFinalizer (fun _ -&gt; async { resource.Close() })</code></example>
    let scopeAsyncFinalizer
        (finalizer: CancellationToken -> Async<unit>)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddFinalizer(fun cancellationToken ->
#if FABLE_COMPILER
                finalizer cancellationToken
#else
                Async.StartAsTask(finalizer cancellationToken) :> Task
#endif
            )
            Execution.ofValue ())

    /// <summary>Creates a flow from an execution outcome.</summary>
    let ofExit (exit: Exit<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofExit exit)

    let internal toExecution
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        : Execution<'value, 'error> =
        runEffect environment cancellationToken flow

    let internal toAsyncInternal
        (environment: 'env)
        (cancellationToken: CancellationToken option)
        (flow: Flow<'env, 'error, 'value>)
        : Async<Exit<'value, 'error>> =
        async {
            let! token =
                match cancellationToken with
                | Some token -> async.Return token
                | None -> Async.CancellationToken

            return! toExecution environment token flow |> Platform.executionToAsync
        }

    #if !FABLE_COMPILER
    let internal toValueTaskInternal
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        : ValueTask<Exit<'value, 'error>> =
        toExecution environment cancellationToken flow

    let internal toTaskInternal
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        : Task<Exit<'value, 'error>> =
        (toExecution environment cancellationToken flow).AsTask()

    #endif

#if !FABLE_COMPILER
    /// <summary>Registers an asynchronous finalizer with the current runtime scope.</summary>
    /// <param name="finalizer">The finalizer to run when the current scope closes.</param>
    /// <returns>A flow that registers the finalizer.</returns>
    /// <remarks>
    /// Use this when a resource acquired by a subflow should live until the surrounding
    /// runtime or layer scope closes, rather than only until the current expression ends.
    /// </remarks>
    let scopeFinalizer
        (finalizer: CancellationToken -> Task)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddFinalizer finalizer
            Execution.ofValue ())

    /// <summary>Registers a disposable resource with the current runtime scope.</summary>
    /// <param name="resource">The disposable resource to close when the current scope closes.</param>
    /// <returns>A flow that registers the resource.</returns>
    let scopeDisposable
        (resource: IDisposable)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddDisposable resource
            Execution.ofValue ())

    /// <summary>Registers an asynchronously disposable resource with the current runtime scope.</summary>
    /// <param name="resource">The async disposable resource to close when the current scope closes.</param>
    /// <returns>A flow that registers the resource.</returns>
    let scopeAsyncDisposable
        (resource: IAsyncDisposable)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddAsyncDisposable resource
            Execution.ofValue ())

#endif

    /// <summary>Acquires a described resource and registers its release with the current runtime scope.</summary>
    /// <param name="resource">The acquisition and release description.</param>
    /// <returns>A flow that succeeds with the acquired value.</returns>
    let scopeResource (Resource(acquire, register)) : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke acquire environment cancellationToken
            |> Execution.bind (fun value ->
                register value (RuntimeState.current().Scope)
                Execution.ofValue value))

#if !FABLE_COMPILER
    /// <summary>Acquires a value and registers its release with the current runtime scope.</summary>
    /// <param name="acquire">The flow that acquires the value.</param>
    /// <param name="release">The release action run when the current scope closes.</param>
    /// <returns>A flow that succeeds with the acquired value.</returns>
    let scopeAcquireRelease
        (acquire: Flow<'env, 'error, 'resource>)
        (release: 'resource -> CancellationToken -> Task)
        : Flow<'env, 'error, 'resource> =
        Resource.create acquire release |> scopeResource
#endif

    /// <summary>Runs a flow in a child scope and closes that scope before returning.</summary>
    /// <remarks>Resources and fibers acquired inside the flow are released after success, typed failure, defect, or interruption without waiting for the surrounding application scope to close.</remarks>
    let scoped (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let parentRuntime = RuntimeState.current()
            let childScope = parentRuntime.Scope.AddChild()
            let childRuntime = parentRuntime |> RuntimeContext.withScope childScope

            Platform.runScoped
                childScope.Close
                cancellationToken
                (fun () -> RuntimeState.withRuntime childRuntime (fun () -> invoke flow environment cancellationToken))
                (fun cleanupError executionError exit ->
                    combineCleanup cleanupError executionError exit "Scoped flow execution produced no outcome."))

    /// <summary>Creates a flow from a raw async operation.</summary>
    /// <remarks>Thrown exceptions are recorded as defects (<c>Cause.Die</c>). Cancellation is recorded as interruption when the runtime token requested it, and as a defect otherwise. Use <c>attemptAsync</c> when expected exceptions should enter the typed error channel.</remarks>
    /// <platforms>Fable compatible</platforms>
    let fromAsync (operation: Async<'value>) : Flow<'env, 'error, 'value> =
        AsyncInterop.from Exit.Success operation

    /// <summary>Creates a flow from an async operation whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>Thrown exceptions are recorded as defects. Cancellation is recorded as interruption when the runtime token requested it, and as a defect otherwise.</remarks>
    /// <platforms>Fable compatible</platforms>
    let fromAsyncResult (operation: Async<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        AsyncInterop.from Exit.fromResult operation

    /// <summary>Creates a flow from an async operation and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Successful completion returns <c>Exit.Success</c>. <c>OperationCanceledException</c> returns <c>Cause.Interrupt</c> when the runtime token requested it. Other exceptions, including cancellation the operation raised for its own reasons, return <c>Cause.Fail exn</c>.</remarks>
    /// <platforms>Fable compatible</platforms>
    let attemptAsync (operation: Async<'value>) : Flow<'env, exn, 'value> =
        Flow(fun _ cancellationToken ->
            Platform.tryExecution
                (fun () -> operation |> Platform.executionOfAsyncUnguarded cancellationToken Exit.Success)
                (fun error ->
                    if ForeignCancellation.isOurs cancellationToken error then
                        Platform.ofExit (Exit.Failure Cause.Interrupt)
                    else
                        Platform.ofExit (Exit.Failure(Cause.Fail error))))

    // Runs synchronous, blocking work off the caller's thread so it does not stall the workflow's continuations.
    // Blocking code cannot be abandoned safely, so once started the work always runs to completion and the flow
    // waits for it; the token lets the operation stop early if it observes cancellation.
    let private runBlocking
        (operation: CancellationToken -> 'source)
        (mapExit: 'source -> Exit<'value, 'error>)
        (onError: CancellationToken -> exn -> Cause<'error>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            let run () =
                try
                    mapExit (operation cancellationToken)
                with error ->
                    Exit.Failure(onError cancellationToken error)

            if cancellationToken.IsCancellationRequested then
                Execution.ofCause Cause.Interrupt
            else
#if FABLE_COMPILER
                Execution.ofExit (run ())
#else
                ValueTask<Exit<'value, 'error>>(Task.Run(run, CancellationToken.None))
#endif
        )

    /// <summary>Creates a flow from synchronous, blocking work, such as a database driver or native library call.</summary>
    /// <remarks>
    /// On .NET the operation runs on the thread pool, so a blocking call does not stall the thread that runs the
    /// workflow. Once started it runs to completion even if the workflow is interrupted, because blocking work
    /// cannot be abandoned safely; it receives the runtime's token so it can stop early. Thrown exceptions are
    /// defects (<c>Cause.Die</c>); cancellation the token requested is an interruption. On JavaScript the
    /// operation runs inline.
    /// </remarks>
    /// <param name="operation">The blocking operation, observing the supplied cancellation token where it can.</param>
    /// <platforms>Fable compatible</platforms>
    /// <example>
    /// <code>
    /// let commits = Flow.fromBlocking (fun _ -> repository.Commits |> Seq.truncate 50 |> List.ofSeq)
    /// </code>
    /// </example>
    let fromBlocking (operation: CancellationToken -> 'value) : Flow<'env, 'error, 'value> =
        runBlocking operation Exit.Success ForeignCancellation.causeOf

    /// <summary>Creates a flow from blocking work whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>Runs like <c>fromBlocking</c>. Thrown exceptions are defects.</remarks>
    /// <param name="operation">The blocking operation, observing the supplied cancellation token where it can.</param>
    /// <platforms>Fable compatible</platforms>
    let fromBlockingResult (operation: CancellationToken -> Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        runBlocking operation Exit.fromResult ForeignCancellation.causeOf

    /// <summary>Creates a flow from blocking work and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>
    /// Runs like <c>fromBlocking</c>. Thrown exceptions return <c>Cause.Fail exn</c>, except cancellation the
    /// runtime token requested, which is an interruption.
    /// </remarks>
    /// <param name="operation">The blocking operation, observing the supplied cancellation token where it can.</param>
    /// <platforms>Fable compatible</platforms>
    let attemptBlocking (operation: CancellationToken -> 'value) : Flow<'env, exn, 'value> =
        runBlocking operation Exit.Success (fun cancellationToken error ->
            if ForeignCancellation.isOurs cancellationToken error then Cause.Interrupt else Cause.Fail error)

#if !FABLE_COMPILER
    // -----------------------------------------------------------------------------------------
    // Task interop.
    //
    // The factory forms are the default: they keep the flow a cold description, run again on every
    // execution, and receive the runtime's cancellation token. The `awaitStarted*` forms wrap work
    // that is already in flight and are named so the call site says so.
    // -----------------------------------------------------------------------------------------

    /// <summary>Creates a flow from a cancellable task factory.</summary>
    /// <remarks>
    /// The factory runs on each execution and receives the runtime's cancellation token, so the flow
    /// stays cold and cancellable. Thrown exceptions are recorded as defects (<c>Cause.Die</c>).
    /// Use <c>attemptTask</c> when expected exceptions should enter the typed error channel.
    /// </remarks>
    /// <param name="factory">Starts the operation, observing the supplied cancellation token.</param>
    /// <platforms>.NET only</platforms>
    /// <example>
    /// <code>
    /// let flow = Flow.fromTask (fun token -> client.GetStringAsync(url, token))
    /// </code>
    /// </example>
    let fromTask (factory: CancellationToken -> Task<'value>) : Flow<'env, 'error, 'value> =
        TaskInterop.from Exit.Success factory

    /// <summary>Creates a flow from a cold task factory whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>The factory runs on each execution and receives the runtime cancellation token. Thrown exceptions are defects.</remarks>
    /// <platforms>.NET only</platforms>
    let fromTaskResult
        (factory: CancellationToken -> Task<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        TaskInterop.from Exit.fromResult factory

    /// <summary>Creates a flow from a cancellable task factory and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Successful completion returns <c>Exit.Success</c>. <c>OperationCanceledException</c> returns <c>Cause.Interrupt</c> when the runtime token requested it. Other exceptions, including cancellation the operation raised for its own reasons, return <c>Cause.Fail exn</c>.</remarks>
    /// <param name="factory">Starts the operation, observing the supplied cancellation token.</param>
    /// <platforms>.NET only</platforms>
    let attemptTask (factory: CancellationToken -> Task<'value>) : Flow<'env, exn, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, exn>>(
                task {
                    try
                        let! value = factory cancellationToken
                        return Exit.Success value
                    with
                    | error when ForeignCancellation.isOurs cancellationToken error ->
                        return Exit.Failure Cause.Interrupt
                    | error ->
                        return Exit.Failure (Cause.Fail error)
                }))

    /// <summary>Creates a flow from a cancellable value-task factory.</summary>
    /// <remarks>Thrown exceptions are recorded as defects (<c>Cause.Die</c>). Use <c>attemptValueTask</c> when expected exceptions should enter the typed error channel.</remarks>
    /// <param name="factory">Starts the operation, observing the supplied cancellation token.</param>
    /// <platforms>.NET only</platforms>
    let fromValueTask (factory: CancellationToken -> ValueTask<'value>) : Flow<'env, 'error, 'value> =
        ValueTaskInterop.from Exit.Success factory

    /// <summary>Creates a flow from a cold value-task factory whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>The factory runs on each execution and receives the runtime cancellation token. Thrown exceptions are defects.</remarks>
    /// <platforms>.NET only</platforms>
    let fromValueTaskResult
        (factory: CancellationToken -> ValueTask<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        ValueTaskInterop.from Exit.fromResult factory

    /// <summary>Creates a flow from a cancellable value-task factory and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Successful completion returns <c>Exit.Success</c>. <c>OperationCanceledException</c> returns <c>Cause.Interrupt</c> when the runtime token requested it. Other exceptions, including cancellation the operation raised for its own reasons, return <c>Cause.Fail exn</c>.</remarks>
    /// <param name="factory">Starts the operation, observing the supplied cancellation token.</param>
    /// <platforms>.NET only</platforms>
    let attemptValueTask (factory: CancellationToken -> ValueTask<'value>) : Flow<'env, exn, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, exn>>(
                task {
                    try
                        let! value = (factory cancellationToken).AsTask()
                        return Exit.Success value
                    with
                    | error when ForeignCancellation.isOurs cancellationToken error ->
                        return Exit.Failure Cause.Interrupt
                    | error ->
                        return Exit.Failure (Cause.Fail error)
                }))

    /// <summary>Observes a task that has already been started.</summary>
    /// <remarks>
    /// The operation is in flight before this is called. It therefore starts outside the workflow,
    /// ignores the runtime's cancellation token, and yields the same single result no matter how
    /// many times the flow is executed. Prefer <c>fromTask</c>, which keeps the flow cold.
    /// Thrown exceptions are recorded as defects (<c>Cause.Die</c>).
    /// </remarks>
    /// <param name="startedTask">A task that is already running.</param>
    /// <platforms>.NET only</platforms>
    let awaitStartedTask (startedTask: Task<'value>) : Flow<'env, 'error, 'value> =
        TaskInterop.from Exit.Success (fun _ -> startedTask)

    /// <summary>Observes an already-started task whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>The work started outside Flow and cannot receive the runtime cancellation token. Thrown exceptions are defects.</remarks>
    /// <platforms>.NET only</platforms>
    let awaitStartedTaskResult
        (startedTask: Task<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        TaskInterop.from Exit.fromResult (fun _ -> startedTask)

    /// <summary>Observes a task that has already been started and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Carries the same caveats as <c>awaitStartedTask</c>.</remarks>
    /// <param name="startedTask">A task that is already running.</param>
    /// <platforms>.NET only</platforms>
    let attemptStartedTask (startedTask: Task<'value>) : Flow<'env, exn, 'value> =
        attemptTask (fun _ -> startedTask)

    /// <summary>Observes a value task that has already been started.</summary>
    /// <remarks>Carries the same caveats as <c>awaitStartedTask</c>.</remarks>
    /// <param name="startedValueTask">A value task that is already running.</param>
    /// <platforms>.NET only</platforms>
    let awaitStartedValueTask (startedValueTask: ValueTask<'value>) : Flow<'env, 'error, 'value> =
        ValueTaskInterop.from Exit.Success (fun _ -> startedValueTask)

    /// <summary>Observes an already-started value task whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>The work started outside Flow and cannot receive the runtime cancellation token. Thrown exceptions are defects.</remarks>
    /// <platforms>.NET only</platforms>
    let awaitStartedValueTaskResult
        (startedValueTask: ValueTask<Result<'value, 'error>>)
        : Flow<'env, 'error, 'value> =
        ValueTaskInterop.from Exit.fromResult (fun _ -> startedValueTask)

    /// <summary>Observes a value task that has already been started and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Carries the same caveats as <c>awaitStartedTask</c>.</remarks>
    /// <param name="startedValueTask">A value task that is already running.</param>
    /// <platforms>.NET only</platforms>
    let attemptStartedValueTask (startedValueTask: ValueTask<'value>) : Flow<'env, exn, 'value> =
        attemptValueTask (fun _ -> startedValueTask)
#endif

    /// <summary>Creates a successful synchronous flow.</summary>
    /// <param name="value">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    let ok (value: 'value) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofValue value)

    /// <summary>Same as <c>ok</c>; the name ZIO uses.</summary>
    /// <param name="value">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    /// <example>
    /// <code>
    /// let result = Flow.succeed 42 |> Flow.run ()
    /// // result = Success 42
    /// </code>
    /// </example>
    let succeed (value: 'value) : Flow<'env, 'error, 'value> =
        ok value

    /// <summary>Creates a failing synchronous flow.</summary>
    /// <param name="failure">The error value to wrap in a failing flow.</param>
    /// <returns>A flow that always fails with the provided error.</returns>
    let error (failure: 'error) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofError failure)

    /// <summary>Same as <c>error</c>; the name ZIO uses.</summary>
    /// <param name="failure">The error value to wrap in a failing flow.</param>
    /// <returns>A flow that always fails with the provided error.</returns>
    /// <example>
    /// <code>
    /// let result = Flow.fail "error" |> Flow.run ()
    /// // result = Failure (Cause.Fail "error")
    /// </code>
    /// </example>
    let fail (failure: 'error) : Flow<'env, 'error, 'value> =
        error failure

    /// <summary>Creates a defective flow that fails with an exception.</summary>
    /// <param name="exn">The exception representing the defect.</param>
    /// <returns>A flow that always dies with the provided exception.</returns>
    /// <remarks>
    /// This is the public constructor for non-domain defects. Use <c>fail</c> for expected
    /// typed failures and <c>die</c> when the workflow should surface a bug or panic.
    /// </remarks>
    let die (exn: exn) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofDie exn)

    /// <summary>Lifts a <see cref="T:System.Result`2" /> into a synchronous flow.</summary>
    /// <param name="result">The result value to lift.</param>
    /// <returns>A flow that succeeds or fails based on the result.</returns>
    /// <example>
    /// <code>
    /// Flow.fromResult (Ok "success") |> Flow.run ()
    /// </code>
    /// </example>
    let fromResult (result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofResult result)

    /// <summary>Creates a flow that verifies an input with an environment-aware policy.</summary>
    /// <remarks>
    /// When the Flow runs, <c>verify</c> supplies its current environment to the policy. An <c>Ok</c> result succeeds
    /// with the policy output. An <c>Error</c> result short-circuits the workflow through its typed error channel.
    /// </remarks>
    /// <param name="policy">The reusable verification rule to apply.</param>
    /// <param name="input">The input value to verify.</param>
    /// <returns>A cold flow that succeeds or fails with the policy result.</returns>
    let verify
        (policy: Policy<'env, 'error, 'input, 'output>)
        (input: 'input)
        : Flow<'env, 'error, 'output> =
        Flow(fun environment _ -> policy environment input |> Execution.ofResult)

    let inline private withRuntime
        (mapper: RuntimeContext -> RuntimeContext)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let runtime = mapper (RuntimeState.current())

            RuntimeState.withRuntime runtime (fun () -> invoke flow environment cancellationToken))

    /// <summary>Installs runtime fiber-lifecycle hooks for diagnostics and telemetry.</summary>
    /// <remarks>
    /// The observer is carried implicitly to every fiber forked inside <paramref name="flow" />, so installing
    /// it once at the application edge covers all descendant background work. Hooks receive diagnostic data
    /// only and cannot alter any fiber's outcome; exceptions they throw are swallowed.
    /// </remarks>
    /// <param name="observer">The lifecycle hooks. Start from <c>FiberObserver.none</c> and override what you need.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that runs with the supplied observer in the ambient runtime context.</returns>
    let withFiberObserver
        (observer: FiberObserver)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withObserver observer) flow

    /// <summary>Adds runtime fiber-lifecycle hooks, composing with any observer already installed.</summary>
    /// <remarks>
    /// Unlike <c>withFiberObserver</c>, which replaces the ambient observer, this stacks the new hooks after
    /// the existing ones, so telemetry, metrics, and registry observers can be installed independently. Each
    /// hook is guarded: one that throws cannot fail a fiber or starve the other hooks.
    /// </remarks>
    /// <param name="observer">The lifecycle hooks to add.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that runs with the composed observer in the ambient runtime context.</returns>
    let addFiberObserver
        (observer: FiberObserver)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime
            (fun runtime ->
                RuntimeContext.withObserver (FiberObserver.compose runtime.Observer observer) runtime)
            flow

    /// <summary>Tracks every fiber forked inside the flow in <paramref name="registry" />.</summary>
    /// <remarks>
    /// The registry's observer is composed with any observer already installed, so telemetry hooks and the
    /// registry can coexist from separate installs. Install once at the application edge, keep the registry,
    /// and call <c>registry.Dump()</c> (or <c>registry.Snapshot()</c>) whenever a live fiber tree is needed.
    /// </remarks>
    /// <param name="registry">The registry that receives fiber lifecycle events.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers are tracked in the registry.</returns>
    let withFiberRegistry
        (registry: FiberRegistry)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        addFiberObserver registry.Observer flow

    /// <summary>Installs a runtime annotation sink for integration packages.</summary>
    /// <exclude/>
    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    let withAnnotationSink
        (sink: string -> string -> unit)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withAnnotationSink sink) flow

    /// <summary>Adds a runtime annotation sink, composing with any sink already installed.</summary>
    /// <exclude/>
    /// <remarks>
    /// Unlike <c>withAnnotationSink</c>, which replaces the ambient sink, this tees annotations to the
    /// existing sink first, so nested telemetry regions and user sinks all receive them. Sinks are guarded:
    /// one that throws cannot fail the workflow or starve the others.
    /// </remarks>
    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    let addAnnotationSink
        (sink: string -> string -> unit)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withComposedAnnotationSink sink) flow

    /// <summary>Adds a runtime annotation for the duration of the supplied flow.</summary>
    /// <remarks>
    /// Annotations are runtime metadata for diagnostics, logging, metrics, and tracing. Nested annotations
    /// with the same key override the outer value for the nested flow only.
    /// </remarks>
    /// <param name="name">The annotation key.</param>
    /// <param name="value">The annotation value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that runs with the supplied annotation in the ambient runtime context.</returns>
    let annotate
        (name: string)
        (value: string)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let currentRuntime = RuntimeState.current()
            // A diagnostics sink must never fail the workflow that annotated.
            (try currentRuntime.AnnotationSink name value with _ -> ())
            let runtime = currentRuntime |> RuntimeContext.withAnnotation name value

            RuntimeState.withRuntime runtime (fun () -> invoke flow environment cancellationToken))

    /// <summary>Adds the standard <c>trace_id</c> runtime annotation for the duration of the supplied flow.</summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that runs with the supplied trace id in the ambient runtime context.</returns>
    let withTraceId
        (traceId: string)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        annotate "trace_id" traceId flow

    /// <summary>Reads the current runtime cancellation token.</summary>
    /// <remarks>Pass it to foreign APIs that take a token. To stop at a safe point, use <c>Flow.ensureNotCanceled</c>.</remarks>
    /// <returns>A flow that succeeds with the token supplied at the workflow execution boundary.</returns>
    let cancellationToken<'env, 'error> : Flow<'env, 'error, CancellationToken> =
        Flow(fun _ cancellationToken -> Execution.ofValue cancellationToken)

    /// <summary>Stops with <c>Cause.Interrupt</c> if the runtime's cancellation token has been cancelled.</summary>
    /// <remarks>
    /// A checkpoint for long-running work that does not otherwise observe cancellation, such as a CPU-bound loop.
    /// It never turns cancellation into a typed error: whoever requested the cancellation decides what it means,
    /// by matching <c>Cause.Interrupt</c> on the <c>Exit</c> at the edge.
    /// </remarks>
    /// <returns>A flow that succeeds with unit when cancellation has not been requested.</returns>
    /// <example>
    /// <code>
    /// flow {
    ///     for chunk in chunks do
    ///         do! Flow.ensureNotCanceled
    ///         process chunk
    /// }
    /// </code>
    /// </example>
    let ensureNotCanceled<'env, 'error> : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken ->
            if cancellationToken.IsCancellationRequested then
                Execution.ofCause Cause.Interrupt
            else
                Execution.ofValue ())

    /// <summary>Turns cancellation that a flow raised for its own reasons into a typed error.</summary>
    /// <param name="handler">Maps the cancellation exception into the workflow error type.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose self-inflicted cancellation becomes <c>Cause.Fail</c>.</returns>
    /// <remarks>
    /// Libraries sometimes throw <see cref="OperationCanceledException" /> while the runtime's token is still live,
    /// for example <c>HttpClient</c>'s own timeout. That is a failure, and task interop records it as a defect;
    /// this maps it into the error channel instead. Cancellation the runtime requested is left as
    /// <c>Cause.Interrupt</c>.
    /// </remarks>
    let catchCancellation
        (handler: OperationCanceledException -> 'error)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let convert (error: exn) (otherwise: unit -> Execution<'value, 'error>) =
                match error with
                | :? OperationCanceledException as canceled when not cancellationToken.IsCancellationRequested ->
                    Execution.ofError (handler canceled)
                | _ -> otherwise ()

            Platform.tryExecution
                (fun () ->
                    invoke flow environment cancellationToken
                    |> Execution.fold
                        Execution.ofValue
                        (fun cause ->
                            match cause with
                            | Cause.Die error -> convert error (fun () -> Execution.ofCause cause)
                            | other -> Execution.ofCause other))
                (fun error -> convert error (fun () -> raise error)))

    /// <summary>Suspends the flow for the specified duration, observing cancellation.</summary>
    /// <param name="delay">The duration to sleep.</param>
    /// <returns>A flow that completes after the specified delay, or is interrupted if cancelled first.</returns>
    let sleep (delay: TimeSpan) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken -> Platform.sleepExecution delay cancellationToken)

    /// <summary>Reads the current runtime scope.</summary>
    /// <returns>A flow that succeeds with the scope owned by the current execution boundary.</returns>
    let scope<'env, 'error> : Flow<'env, 'error, Scope> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Scope))

    /// <summary>Reads the current runtime annotations.</summary>
    /// <returns>A flow that succeeds with the ambient annotation map.</returns>
    let annotations<'env, 'error> : Flow<'env, 'error, Map<string, string>> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Annotations))

    /// <summary>Reads the current trace id annotation, if one is present.</summary>
    /// <remarks>Set it with <c>Flow.withTraceId</c>.</remarks>
    /// <returns>A flow that succeeds with the ambient <c>trace_id</c> value, if present.</returns>
    let traceId<'env, 'error> : Flow<'env, 'error, string option> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Annotations |> Map.tryFind "trace_id"))

    /// <summary>Reads the current fiber id from the ambient runtime context.</summary>
    /// <remarks>
    /// The root workflow runs on a fiber id of its own; every <c>Flow.fork</c> child gets a fresh id.
    /// Telemetry integrations use this to correlate workflow spans with fiber lifecycle events.
    /// </remarks>
    /// <returns>A flow that succeeds with the current <see cref="T:Axial.FiberId" />.</returns>
    let fiberId<'env, 'error> : Flow<'env, 'error, FiberId> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().FiberId))

    /// <summary>Fails with the supplied typed error when the flow does not complete before the timeout.</summary>
    /// <remarks>The timed-out flow is interrupted. The timeout owns that interruption, so it may report it as a typed error.</remarks>
    /// <param name="after">The timeout duration.</param>
    /// <param name="timeoutError">The typed error returned when the timeout wins.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that returns the source outcome or the timeout error.</returns>
    let timeout
        (after: TimeSpan)
        (timeoutError: 'error)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            Platform.timeoutExecution
                after
                (invoke flow environment)
                cancellationToken
                (reportDiscardedExit (RuntimeState.current().Observer))
                (fun () -> Platform.ofExit (Exit.Failure(Cause.Fail timeoutError))))

    /// <summary>Returns the supplied success value when the flow does not complete before the timeout.</summary>
    /// <param name="after">The timeout duration.</param>
    /// <param name="value">The success value returned when the timeout wins.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that returns the source outcome or the supplied success value.</returns>
    let timeoutToOk
        (after: TimeSpan)
        (value: 'value)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            Platform.timeoutExecution
                after
                (invoke flow environment)
                cancellationToken
                (reportDiscardedExit (RuntimeState.current().Observer))
                (fun () -> Platform.ofExit (Exit.Success value)))

    /// <summary>Same as <c>timeout</c>; named to pair with <c>timeoutToOk</c>.</summary>
    let timeoutToError
        (after: TimeSpan)
        (error: 'error)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        timeout after error flow

    /// <summary>Runs a fallback flow when the source flow does not complete before the timeout.</summary>
    /// <param name="after">The timeout duration.</param>
    /// <param name="fallback">Creates the fallback flow when the timeout wins.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that returns the source outcome or the fallback outcome.</returns>
    let timeoutWith
        (after: TimeSpan)
        (fallback: unit -> Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            Platform.timeoutExecution
                after
                (invoke flow environment)
                cancellationToken
                (reportDiscardedExit (RuntimeState.current().Observer))
                (fun () -> invoke (fallback ()) environment cancellationToken))

    // Runs one attempt of a retried, repeated, or supervised flow in a child scope of its own, so that an attempt
    // that is superseded by the next one can release what it acquired before that next attempt starts.
    let private runAttempt
        (flow: Flow<'env, 'error, 'value>)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : Scope * Execution<'value, 'error> =
        let parentRuntime = RuntimeState.current()
        let attemptScope = parentRuntime.Scope.AddChild()
        let attemptRuntime = parentRuntime |> RuntimeContext.withScope attemptScope

        let execution =
            Platform.tryExecution
                (fun () -> RuntimeState.withRuntime attemptRuntime (fun () -> invoke flow environment cancellationToken))
                (fun error -> Execution.ofCause (Execution.causeOfException error))

        attemptScope, execution

    // Closes the scope of an attempt that the next attempt supersedes. The final attempt's scope is left attached to
    // the parent: a value it returns (a connection, a file handle) must outlive the retry, so it is released when the
    // enclosing scope closes, exactly as if the flow had not been retried.
    let private closeSuperseded
        (attemptScope: Scope)
        (onCleanupError: exn -> Cause<'error>)
        (cancellationToken: CancellationToken)
        : Execution<unit, 'error> =
        Platform.runScoped
            attemptScope.Close
            cancellationToken
            (fun () -> Execution.ofValue ())
            (fun cleanupError _ _ ->
                match cleanupError with
                | Some error -> Exit.Failure(onCleanupError error)
                | None -> Exit.Success())

    // Closes a superseded attempt's scope and then waits out the rest of the schedule's delay. The deadline is fixed
    // before cleanup starts, so time spent in finalizers is part of the delay rather than added to it; otherwise a
    // fixed-rate schedule would drift by the cleanup time on every run.
    let private supersedeThenWait
        (attemptScope: Scope)
        (onCleanupError: exn -> Cause<'error>)
        (delay: TimeSpan)
        (cancellationToken: CancellationToken)
        : Execution<unit, 'error> =
        let deadline = Platform.monotonicNow () + delay

        closeSuperseded attemptScope onCleanupError cancellationToken
        |> Execution.bind (fun () ->
            let remaining = deadline - Platform.monotonicNow ()
            Platform.sleepExecution (if remaining > TimeSpan.Zero then remaining else TimeSpan.Zero) cancellationToken)

    let private scheduleContext attempt loopStarted executionStarted : ScheduleContext =
        { Attempt = attempt
          LoopStarted = loopStarted
          ExecutionStarted = executionStarted
          ExecutionEnded = Platform.monotonicNow () }

    /// <summary>Retries a flow's typed failures according to a schedule.</summary>
    /// <remarks>
    /// The flow runs once, and after each <c>Cause.Fail</c> the schedule sees the error and decides whether to run
    /// it again and how long to wait. Defects and interruptions are never retried. When the schedule stops, the flow
    /// fails with the last error. Each attempt runs in its own child scope; a failed attempt's finalizers run before
    /// the next attempt starts. For the common case, build the schedule from a <c>Retry</c> record.
    /// </remarks>
    /// <param name="schedule">Decides, from each typed error, whether to retry and after what delay.</param>
    /// <param name="flow">The flow to retry.</param>
    /// <returns>A flow that succeeds as soon as an attempt succeeds.</returns>
    /// <example>
    /// <code>
    /// fetch |&gt; Flow.retry (Schedule.recurs 3)
    ///
    /// fetch |&gt; Flow.retry (Retry.schedule { Retry.defaults with When = HttpError.isTransient })
    /// </code>
    /// </example>
    let retry
        (schedule: Schedule<'env, 'error, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        // A loop rather than recursion, so retrying for the life of an application runs in constant memory.
        Flow(fun environment cancellationToken ->
            let loopStarted = Platform.monotonicNow ()

            Execution.loop (0, loopStarted) (fun (attempt, executionStarted) ->
                let attemptScope, execution = runAttempt flow environment cancellationToken

                execution
                |> Execution.fold
                    (Platform.Break >> Execution.ofValue)
                    (fun cause ->
                        match cause with
                        | Cause.Fail error ->
                            let context = scheduleContext attempt loopStarted executionStarted

                            Schedule.decide schedule error context environment cancellationToken
                            |> Execution.bind (fun (decision, delay) ->
                                match decision with
                                | Some _ ->
                                    supersedeThenWait
                                        attemptScope
                                        (fun cleanupError -> Cause.thenCause cause (Execution.causeOfException cleanupError))
                                        delay
                                        cancellationToken
                                    |> Execution.map (fun () -> Platform.Continue(attempt + 1, Platform.monotonicNow ()))
                                | None -> Execution.ofCause cause)
                        | _ -> Execution.ofCause cause)))

    /// <summary>Repeats a successful flow according to a schedule.</summary>
    /// <remarks>
    /// The flow runs once, and after each success the schedule sees the value and decides whether to run it again
    /// and how long to wait. Any failure stops the repetition immediately. When the schedule stops, the flow
    /// succeeds with the last value. Each run has its own child scope, closed before the next run starts, so a
    /// repetition that lasts the life of an application does not accumulate finalizers.
    /// </remarks>
    /// <param name="schedule">Decides, from each value, whether to repeat and after what delay.</param>
    /// <param name="flow">The flow to repeat.</param>
    /// <returns>A flow that succeeds with the value of the last run.</returns>
    /// <example>
    /// <code>
    /// heartbeat |&gt; Flow.repeat (Schedule.spaced (TimeSpan.FromSeconds 5.0))
    /// </code>
    /// </example>
    let repeat
        (schedule: Schedule<'env, 'value, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        // A loop rather than recursion, so a schedule that repeats for the life of an application (a control
        // scan, a heartbeat) runs in constant memory.
        Flow(fun environment cancellationToken ->
            let loopStarted = Platform.monotonicNow ()
            let firstScope, first = runAttempt flow environment cancellationToken

            first
            |> Execution.bind (fun firstValue ->
                Execution.loop (0, firstValue, firstScope, loopStarted) (fun (attempt, lastValue, lastScope, executionStarted) ->
                    let context = scheduleContext attempt loopStarted executionStarted

                    Schedule.decide schedule lastValue context environment cancellationToken
                    |> Execution.bind (fun (decision, (delay: TimeSpan)) ->
                        match decision with
                        | Some _ ->
                            supersedeThenWait lastScope Execution.causeOfException delay cancellationToken
                            |> Execution.bind (fun () ->
                                let started = Platform.monotonicNow ()
                                let nextScope, next = runAttempt flow environment cancellationToken

                                next
                                |> Execution.map (fun nextValue ->
                                    Platform.Continue(attempt + 1, nextValue, nextScope, started)))
                        | None -> Execution.ofValue (Platform.Break lastValue)))))

    /// <summary>Restarts a flow that terminates with an unexpected defect, according to a schedule.</summary>
    /// <remarks>
    /// The defect-channel sibling of <c>retry</c>: <c>retry</c> re-runs typed <c>Cause.Fail</c> errors and never
    /// touches defects, while <c>supervise</c> re-runs <c>Cause.Die</c> defects and never touches typed errors or
    /// interruptions. The schedule sees the first defect of each failed run. Each attempt runs in its own child
    /// scope, closed before the next attempt starts, so finalizers registered by a failed attempt are released
    /// instead of accumulating. Re-evaluation only resets state that lives inside the flow itself; mutable state in
    /// the environment is not restored. When the schedule stops, the final defect propagates as the flow's exit.
    /// </remarks>
    /// <param name="schedule">Decides, from each defect, whether to restart and after what delay.</param>
    /// <param name="flow">The flow to supervise.</param>
    /// <returns>A flow that re-evaluates <c>Cause.Die</c> outcomes while the schedule allows it.</returns>
    /// <example>
    /// <code>
    /// worker |&gt; Flow.supervise (Retry.schedule { Retry.defaults with Retries = 5 })
    /// </code>
    /// </example>
    let supervise
        (schedule: Schedule<'env, exn, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        // Restart only pure defect outcomes: an interruption must stay an interruption, and a cause that also
        // carries a typed failure must surface it rather than being silently re-run.
        let restartable (cause: Cause<'error>) =
            match Cause.defects cause with
            | first :: _ when not (Cause.isInterrupted cause) && List.isEmpty (Cause.failures cause) -> Some first
            | _ -> None

        Flow(fun environment cancellationToken ->
            let loopStarted = Platform.monotonicNow ()

            Execution.loop (0, loopStarted) (fun (attempt, executionStarted) ->
                let attemptScope, execution = runAttempt flow environment cancellationToken

                execution
                |> Execution.fold
                    (Platform.Break >> Execution.ofValue)
                    (fun cause ->
                        match restartable cause with
                        | Some defect ->
                            let context = scheduleContext attempt loopStarted executionStarted

                            Schedule.decide schedule defect context environment cancellationToken
                            |> Execution.bind (fun (decision, delay) ->
                                match decision with
                                | Some _ ->
                                    supersedeThenWait
                                        attemptScope
                                        (fun cleanupError -> Cause.thenCause cause (Execution.causeOfException cleanupError))
                                        delay
                                        cancellationToken
                                    |> Execution.map (fun () -> Platform.Continue(attempt + 1, Platform.monotonicNow ()))
                                | None -> Execution.ofCause cause)
                        | None -> Execution.ofCause cause)))

    let private forkWith (name: string option) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            let parentRuntime = RuntimeState.current()
            let observer = parentRuntime.Observer

            let metadata: FiberMetadata =
                {
                    Id = FiberId.next ()
                    Name = name
                    ParentId = Some parentRuntime.FiberId
                    Annotations = parentRuntime.Annotations
                    // Fiber lifecycle bookkeeping is scheduler mechanics, not application behavior — the
                    // AGENTS.md effect-boundary invariant carves out "ambient runtime for executor mechanics".
                    StartedAt = DateTimeOffset.UtcNow // axial-allow-effect: clock
                    SettledAt = None
                    Status = FiberStatus.Running
                }

            let tracker = FiberDefectTracker(metadata, observer)
            let childRuntime = parentRuntime |> RuntimeContext.withFiberId metadata.Id

            FiberObserver.notifyStart observer metadata

            // A fiber that settles without a defect has nothing left for its scope to interrupt or report, so it
            // drops its scope registration. Otherwise a long-lived scope would retain every fiber it ever forked.
            // The fiber can settle before registration finishes, so both sides meet under a lock.
            let registrationGate = obj()
            let registration = ref ValueNone
            let settledClean = ref false
            let settled = ref None

            let releaseRegistration () =
                match registration.Value with
                | ValueSome key -> parentRuntime.Scope.Unregister key
                | ValueNone -> ()

            let cts, exitTask =
                Platform.startFiber
                    cancellationToken
                    (fun status exit ->
                        settled.Value <- Some exit
                        // axial-allow-effect: clock
                        metadata.SettledAt <- Some DateTimeOffset.UtcNow
                        metadata.Status <- status

                        let defect =
                            match exit with
                            | Exit.Success _ -> None
                            | Exit.Failure cause -> Cause.defects cause |> List.tryHead

                        tracker.Settled defect

                        if defect.IsNone then
                            Platform.lock registrationGate (fun () ->
                                settledClean.Value <- true
                                releaseRegistration ())

                        FiberObserver.notifyEnd observer metadata defect)
                    (fun childToken ->
                        RuntimeState.withRuntime childRuntime (fun () -> invoke flow environment childToken))

            // A fiber belongs to the scope that forked it. Closing that scope interrupts the fiber,
            // waits for its cleanup, then reports an unobserved defect deterministically.
            try
#if FABLE_COMPILER
                let key =
                    parentRuntime.Scope.Register(fun _ -> async {
                        cts.Cancel()
                        let! _ = exitTask
                        tracker.TryReport()
                    })
#else
                let weakTracker = WeakReference<FiberDefectTracker>(tracker)

                let key =
                    parentRuntime.Scope.Register(fun _ -> task {
                        cts.Cancel()
                        let! _ = exitTask
                        match weakTracker.TryGetTarget() with
                        | true, live -> live.TryReport()
                        | _ -> ()
                    })
#endif
                Platform.lock registrationGate (fun () ->
                    registration.Value <- ValueSome key

                    if settledClean.Value then
                        releaseRegistration ())
            with _ -> ()

            let fiber = Fiber(metadata, exitTask, cts, tracker, settled)

#if !FABLE_COMPILER
            // GC net: keep the tracker alive exactly as long as the fiber handle is reachable. When a
            // discarded handle is collected, the tracker becomes collectable and its finalizer reports the
            // defect even if the forking scope never closes.
            FiberDefectTracker.Attach(fiber, tracker)
#endif

            Execution.ofValue fiber)

    /// <summary>Starts a flow in a new fiber without waiting for it to complete.</summary>
    /// <remarks>
    /// Forking turns a cold flow description into hot child work and returns a handle
    /// that can later be joined or interrupted. Prefer <c>zipPar</c> or <c>race</c>
    /// when the caller only needs a simple parallel composition. Wait for the handle with <c>Fiber.join</c> or
    /// <c>Fiber.await</c>, and stop it with <c>Fiber.interrupt</c>.
    /// </remarks>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:Axial.Fiber`2" /> handle.</returns>
    let fork (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkWith None flow

    /// <summary>Starts a flow in a new fiber carrying a diagnostic name.</summary>
    /// <remarks>
    /// The name appears in <c>FiberDump</c> snapshots, <c>FiberRegistry</c> dumps, and telemetry fiber spans,
    /// so long-lived background fibers are recognizable in diagnostics instead of showing as bare ids.
    /// </remarks>
    /// <param name="name">The diagnostic name recorded in the fiber's metadata.</param>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:Axial.Fiber`2" /> handle.</returns>
    let forkNamed (name: string) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkWith (Some name) flow

    /// <summary>Starts a flow in a new fiber that is deliberately never awaited.</summary>
    /// <remarks>
    /// The explicit fire-and-forget: the fiber counts as observed from birth, so a defect it dies with is
    /// never reported as an unobserved defect through the runtime's fiber observer. Use this instead of
    /// discarding a <c>Flow.fork</c> handle when silence is intended; a discarded <c>fork</c> handle whose
    /// fiber dies of a defect is reported.
    /// </remarks>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:Axial.Fiber`2" /> handle that can still be joined or interrupted.</returns>
    let forkDetached (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            invoke (fork flow) environment cancellationToken
            |> Execution.fold
                (fun (fiber: Fiber<'error, 'value>) ->
                    fiber.MarkObserved()
                    Execution.ofValue fiber)
                Execution.ofCause)

    /// <summary>Combines two flows into a tuple of their values, running them concurrently.</summary>
    /// <remarks>
    /// If either flow fails, the other is interrupted immediately.
    /// </remarks>
    /// <param name="left">The first flow to combine.</param>
    /// <param name="right">The second flow to combine.</param>
    /// <returns>A flow that returns a tuple of both successful values.</returns>
    /// <example>
    /// <code>
    /// let combined = Flow.zipPar (Flow.succeed 1) (Flow.succeed 2)
    /// combined |> Flow.run ()
    /// </code>
    /// </example>
    let zipPar
        (left: Flow<'env, 'error, 'left>)
        (right: Flow<'env, 'error, 'right>)
        : Flow<'env, 'error, 'left * 'right> =
        Flow(fun environment cancellationToken ->
            Platform.zipParExecution
                (invoke left environment)
                (invoke right environment)
                cancellationToken
                chooseParallelExit)

    /// <summary>Runs all flows concurrently and returns their values in input order.</summary>
    /// <remarks>An empty input succeeds immediately. If any flow fails, remaining flows are interrupted through the same structured parallel composition as <c>zipPar</c>.</remarks>
    let rec sequencePar (flows: Flow<'env, 'error, 'value> list) : Flow<'env, 'error, 'value list> =
        let mapValue mapper flow =
            Flow(fun environment cancellationToken -> invoke flow environment cancellationToken |> Execution.map mapper)

        match flows with
        | [] -> ok []
        | [ flow ] -> mapValue List.singleton flow
        | _ ->
            let midpoint = flows.Length / 2
            let left, right = List.splitAt midpoint flows
            zipPar (sequencePar left) (sequencePar right)
            |> mapValue (fun (leftValues, rightValues) -> leftValues @ rightValues)

    /// <summary>Runs two flows concurrently and returns the result of the first one to complete.</summary>
    /// <remarks>
    /// The "loser" flow is interrupted immediately.
    /// </remarks>
    /// <param name="left">The first flow to run.</param>
    /// <param name="right">The second flow to run.</param>
    /// <returns>A flow containing the result of the first flow to complete.</returns>
    /// <example>
    /// <code>
    /// let fastOrSlow = Flow.race (Flow.succeed "cached") (Flow.succeed "loaded")
    /// fastOrSlow |> Flow.run ()
    /// </code>
    /// </example>
    let race
        (left: Flow<'env, 'error, 'value>)
        (right: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            Platform.raceExecution
                (invoke left environment)
                (invoke right environment)
                cancellationToken
                (reportDiscardedExit (RuntimeState.current().Observer)))

    /// <summary>Lifts an option into a synchronous flow with the supplied error.</summary>
    /// <param name="error">The error to return if the option is <c>None</c>.</param>
    /// <param name="value">The option to lift.</param>
    /// <returns>A flow that succeeds with the option's value or fails with the provided error.</returns>
    /// <example>
    /// <code>
    /// let opt = Some "value"
    /// Flow.fromOption "missing" opt |> Flow.run ()
    /// </code>
    /// </example>
    let fromOption (error: 'error) (value: 'value option) : Flow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResult error
        |> fromResult

    /// <summary>Lifts a value option into a synchronous flow with the supplied error.</summary>
    /// <param name="error">The error to return if the value option is <see cref="T:Microsoft.FSharp.Core.FSharpValueOption`1.ValueNone" />.</param>
    /// <param name="value">The value option to lift.</param>
    /// <returns>A <see cref="T:Axial`3" /> that succeeds with the option's value or fails with the provided error.</returns>
    let fromValueOption (error: 'error) (value: 'value voption) : Flow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    /// <summary>Lifts a result that failed without a reason, taking the error from a flow that runs only on failure.</summary>
    /// <remarks>
    /// <para>
    /// The <c>unit</c> error is not an empty error type — it is the absence of a reason. <c>Result.okIf</c> and
    /// <c>Result.failIf</c> report that a value failed a predicate and deliberately nothing else, leaving the reason
    /// to a separate step. <c>Result.orError</c> is that step for a constant; this is that step when producing the
    /// error needs the environment, as a localized message, a correlation id, or a configured code does.
    /// </para>
    /// <para>
    /// Pinning the source to <c>unit</c> is what makes <paramref name="errorFlow" /> the only possible source of the
    /// error. A result that already carries one keeps it: map it with <c>Result.mapError</c> and use
    /// <c>Flow.fromResult</c>.
    /// </para>
    /// </remarks>
    /// <param name="errorFlow">A flow that reads the environment to produce an error value.</param>
    /// <param name="result">The pure result to bridge.</param>
    /// <returns>A <see cref="T:Axial`3" /> that mirrors the success of the result or fails with the outcome of the error flow.</returns>
    /// <example>
    /// <code>
    /// let result = Result.Error ()
    /// let flow = Flow.fromResultOr (Flow.envWith (fun env -> "error")) result
    /// </code>
    /// </example>
    let fromResultOr
        (errorFlow: Flow<'env, 'error, 'error>)
        (result: Result<'value, unit>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            match result with
            | Ok value -> Execution.ofValue value
            | Error () ->
                invoke errorFlow environment cancellationToken
                |> Execution.fold Execution.ofError Execution.ofCause)

    /// <summary>Reads the current environment as the successful flow value.</summary>
    /// <remarks>

    /// <summary>Reads the current environment as the successful flow value.</summary>
    /// <remarks>
    /// Use this when the next step genuinely needs the whole environment value, for example when
    /// passing a request context to another helper. For a single dependency or configuration value,
    /// prefer <c>Flow.envWith</c>; it keeps the dependency local and makes the workflow easier to scan.
    /// </remarks>
    /// <returns>A <see cref="T:Axial`3" /> whose successful value is the current environment.</returns>
    /// <example>
    /// <code>
    /// let myFlow = Flow.env |> Flow.map (fun env -> env)
    /// </code>
    /// </example>
    let env<'env, 'error> : Flow<'env, 'error, 'env> =
        Flow(fun environment _ -> Execution.ofValue environment)

    /// <summary>Projects one value from the current environment.</summary>
    /// <remarks>
    /// This is the primary way to access app dependencies, configuration, or request metadata stored
    /// in <c>env</c>. The projection runs only when the flow is executed, so constructing the flow is
    /// still pure and side-effect free. Prefer small projections over passing a large environment
    /// deeper into reusable helpers.
    /// </remarks>
    /// <param name="projection">A function that extracts a value from the environment.</param>
    /// <returns>A <see cref="T:Axial`3" /> containing the projected value.</returns>
    /// <example>
    /// <code>
    /// let currentTime () =
    ///     Flow.envWith (fun (environment: BaseRuntime) -> environment.Clock.UtcNow())
    /// </code>
    /// </example>
    let envWith (projection: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow(fun environment _ -> Execution.ofValue (projection environment))

    /// <summary>Transforms the successful value of a flow.</summary>
    /// <remarks>
    /// If the source <paramref name="flow" /> fails, the <paramref name="mapper" /> is not executed.
    /// The original failure cause is preserved, including typed failures, interruption, and defects.
    /// Use <c>map</c> for pure value transformations after an effect has succeeded.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the successful value.</param>
    /// <param name="flow">The source flow of type <see cref="T:Axial`3" /> to transform.</param>
    /// <returns>A new <see cref="T:Axial`3" /> with the transformed success value of type <c>'next</c>.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 1 |> Flow.map (fun x -> x + 1)
    /// </code>
    /// </example>
    let map
        (mapper: 'value -> 'next)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.map mapper)

    /// <summary>Maps the successful value of a synchronous flow to <c>unit</c>.</summary>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that succeeds with <c>unit</c> instead of the original value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 42 |> Flow.ignore
    /// </code>
    /// </example>
    let ignore (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, unit> =
        map (fun _ -> ()) flow

    /// <summary>Sequences a dependent flow after a successful value.</summary>
    /// <remarks>
    /// This is the flatmap operation for <see cref="T:Axial`3" />. The continuation only runs
    /// when the source flow succeeds, and it receives the successful value. Use <c>bind</c> when the
    /// next effect depends on the previous result; use <c>map</c> when the next step is pure.
    /// </remarks>
    /// <param name="binder">A function that takes the successful value and returns a new flow.</param>
    /// <param name="flow">The source flow to sequence.</param>
    /// <returns>A <see cref="T:Axial`3" /> representing the combined workflow.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 1 |> Flow.bind (fun x -> Flow.succeed (x + 1))
    /// </code>
    /// </example>
    let bind
        (binder: 'value -> Flow<'env, 'error, 'next>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.bind (fun value -> invoke (binder value) environment cancellationToken))

    /// <summary>Sequences a synchronous continuation after a successful value.</summary>
    let (>>=)
        (flow: Flow<'env, 'error, 'value>)
        (binder: 'value -> Flow<'env, 'error, 'next>)
        : Flow<'env, 'error, 'next> =
        bind binder flow

    /// <summary>Runs an effect on success and preserves the original value.</summary>
    /// <remarks>
    /// Use this for logging, telemetry, metrics, or audit steps that should observe a successful
    /// value without replacing it. If the <paramref name="binder" /> flow fails, that failure becomes
    /// the result of the whole flow, because the tap effect is still part of the workflow.
    /// </remarks>
    /// <param name="binder">A function that produces a side-effect flow from the successful value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A <see cref="T:Axial`3" /> that preserves the original success value after the side effect.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 42 |> Flow.tap (fun x -> Flow.succeed ())
    /// </code>
    /// </example>
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
    /// <returns>A <see cref="T:Axial`3" /> that preserves the original error after the side effect.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error" |> Flow.tapError (fun err -> Flow.succeed ())
    /// </code>
    /// </example>
    let tapError
        (binder: 'error -> Flow<'env, 'error, unit>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.fold
                Execution.ofValue
                (fun cause ->
                    match cause with
                    | Cause.Fail error ->
                        invoke (binder error) environment cancellationToken
                        |> Execution.fold
                            (fun () -> Execution.ofCause cause)
                            Execution.ofCause
                    | _ -> Execution.ofCause cause))

    /// <summary>Maps the error value of a synchronous flow.</summary>
    /// <remarks>
    /// Transforms the error type of the flow while leaving successful values untouched.
    /// Useful for mapping internal errors into public-facing domain errors.
    /// </remarks>
    /// <param name="mapper">The function to transform the error value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A <see cref="T:Axial`3" /> with the transformed error type.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error" |> Flow.mapError (fun err -> err + "!")
    /// </code>
    /// </example>
    let mapError
        (mapper: 'error -> 'nextError)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.mapError mapper)

    /// <summary>Attaches diagnostic trace text to any failure cause of the flow.</summary>
    /// <remarks>
    /// On failure the cause is wrapped in <c>Cause.Traced</c>, so retries, parallel composition, and
    /// telemetry (<c>Cause.prettyPrint</c>, the <c>axial.flow.cause</c> span tag) can show where in the
    /// workflow the failure passed through. Successful values are untouched, and the typed error is not
    /// changed — only the cause tree grows a trace node.
    /// </remarks>
    /// <param name="trace">The diagnostic trace text, typically an operation or boundary name.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose failures carry the trace annotation.</returns>
    /// <example>
    /// <code>
    /// let flow = loadUser |> Flow.tracedError "billing.load-user"
    /// </code>
    /// </example>
    let tracedError
        (trace: string)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.mapBoth id (Cause.traced trace))

    /// <summary>Maps both the successful value and the failure cause of a synchronous flow.</summary>
    /// <param name="onSuccess">The function to transform the success value.</param>
    /// <param name="onFailure">The function to transform the failure cause.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A new <see cref="T:Axial`3" /> with transformed success and error types.</returns>
    let mapBoth
        (onSuccess: 'value -> 'next)
        (onFailure: Cause<'error> -> Cause<'nextError>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.mapBoth onSuccess onFailure)

    /// <summary>Folds both the successful value and the failure cause into a new flow.</summary>
    /// <remarks>
    /// This is the most powerful combinator for branching logic based on the full outcome of a flow,
    /// including interruptions and defects.
    /// </remarks>
    /// <param name="onSuccess">A function that returns a new flow from the success value.</param>
    /// <param name="onFailure">A function that returns a new flow from the failure cause.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that continues with the outcome of either <paramref name="onSuccess" /> or <paramref name="onFailure" />.</returns>
    let fold
        (onSuccess: 'value -> Flow<'env, 'nextError, 'next>)
        (onFailure: Cause<'error> -> Flow<'env, 'nextError, 'next>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.fold
                (fun value -> invoke (onSuccess value) environment cancellationToken)
                (fun cause -> invoke (onFailure cause) environment cancellationToken))

    /// <summary>Catches exceptions raised during execution and simple defect outcomes, then maps them to a typed error.</summary>
    /// <remarks>
    /// Thrown exceptions and simple <c>Cause.Die</c> outcomes are converted to <c>Cause.Fail</c>.
    /// Existing typed failures and interruptions are preserved, and an <c>OperationCanceledException</c> thrown
    /// because the runtime's token was cancelled stays an interruption. Compound causes are preserved unchanged.
    /// </remarks>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source flow of type <see cref="T:Axial`3" /> to monitor.</param>
    /// <returns>A <see cref="T:Axial`3" /> that converts recoverable exceptions into typed errors.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.die (System.Exception("boom")) |> Flow.catch (fun ex -> "caught: " + ex.Message)
    /// </code>
    /// </example>
    let catch
        (handler: exn -> 'error)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            Platform.tryExecution
                (fun () ->
                    invoke flow environment cancellationToken
                    |> Execution.fold
                        (fun value -> Execution.ofValue value)
                        (fun cause ->
                            match cause with
                            | Cause.Die error -> Execution.ofCause (Cause.Fail(handler error))
                            | other -> Execution.ofCause other))
                (fun error ->
                    if ForeignCancellation.isOurs cancellationToken error then
                        Platform.ofExit (Exit.Failure Cause.Interrupt)
                    else
                        Platform.ofExit (Exit.Failure(Cause.Fail(handler error)))))

    /// <summary>Computes a fallback flow from the typed error when the source flow fails.</summary>
    /// <remarks>
    /// The fallback runs only for expected typed failures represented by <c>Cause.Fail</c>. It does
    /// not catch interruption or defects. Use this for domain-level recovery, not for swallowing
    /// cancellation or unexpected exceptions.
    /// </remarks>
    /// <param name="fallback">A function that produces a new flow from the error value.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that recovers from errors using the fallback function.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error" |> Flow.orElseWith (fun err -> Flow.succeed "recovered")
    /// </code>
    /// </example>
    let orElseWith
        (fallback: 'error -> Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.fold
                Execution.ofValue
                (fun cause ->
                    match cause with
                    | Cause.Fail error -> invoke (fallback error) environment cancellationToken
                    | _ -> Execution.ofCause cause))

    /// <summary>Falls back to another flow when the source flow fails.</summary>
    /// <param name="fallback">The flow to run if the source flow fails.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that recovers from errors using the fallback flow.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error" |> Flow.orElse (Flow.succeed "recovered")
    /// </code>
    /// </example>
    let orElse
        (fallback: Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        orElseWith (fun _ -> fallback) flow

    /// <summary>Runs two flows sequentially and combines their successful values into a tuple.</summary>
    /// <param name="left">The first flow to run.</param>
    /// <param name="right">The second flow to run.</param>
    /// <returns>A flow that returns a tuple of both successful values.</returns>
    /// <example>
    /// <code>
    /// Flow.zip (Flow.succeed 1) (Flow.succeed 2) |> Flow.run ()
    /// </code>
    /// </example>
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
    /// <param name="mapper">A function that combines the successful values of both flows.</param>
    /// <param name="left">The first flow to run.</param>
    /// <param name="right">The second flow to run.</param>
    /// <returns>A flow containing the mapped value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.map2 (fun x y -> x + y) (Flow.succeed 1) (Flow.succeed 2)
    /// </code>
    /// </example>
    let map2
        (mapper: 'left -> 'right -> 'value)
        (left: Flow<'env, 'error, 'left>)
        (right: Flow<'env, 'error, 'right>)
        : Flow<'env, 'error, 'value> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies a flow-wrapped function to a flow-wrapped value.</summary>
    /// <param name="flow">A flow that contains a function to apply.</param>
    /// <param name="value">A flow that contains the value to apply the function to.</param>
    /// <returns>A flow containing the result of applying the function to the value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.apply (Flow.succeed (fun x -> x + 1)) (Flow.succeed 1)
    /// </code>
    /// </example>
    let apply
        (flow: Flow<'env, 'error, 'value -> 'next>)
        (value: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        map2 (fun mapper input -> mapper input) flow value

    /// <summary>Combines three flows with a mapping function.</summary>
    /// <param name="mapper">A function that combines the successful values of all three flows.</param>
    /// <param name="left">The first flow to run.</param>
    /// <param name="middle">The second flow to run.</param>
    /// <param name="right">The third flow to run.</param>
    /// <returns>A flow containing the mapped value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.map3 (fun x y z -> x + y + z) (Flow.succeed 1) (Flow.succeed 2) (Flow.succeed 3)
    /// </code>
    /// </example>
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

    /// <summary>Runs a flow against an environment derived from the outer environment.</summary>
    /// <remarks>
    /// Use this to embed a smaller workflow inside a larger application environment without changing
    /// the smaller workflow's type. The mapping is applied at execution time. This is useful for
    /// preserving narrow helper signatures while still running everything from one app boundary.
    /// </remarks>
    /// <param name="mapping">A function that maps the outer environment to the inner environment.</param>
    /// <param name="flow">The flow to run with the inner environment.</param>
    /// <returns>A flow that expects the outer environment.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 1 |> Flow.localEnv (fun outer -> outer)
    /// </code>
    /// </example>
    let localEnv
        (mapping: 'outerEnvironment -> 'innerEnvironment)
        (flow: Flow<'innerEnvironment, 'error, 'value>)
        : Flow<'outerEnvironment, 'error, 'value> =
        Flow(fun environment ct ->
            let innerEnvironment = mapping environment
            invoke flow innerEnvironment ct)

    /// <summary>Runs a provisioning step in a fresh scope, then the downstream flow, closing the scope after.</summary>
    /// <remarks>
    /// Internal seam for <c>Axial.Layers</c>. The provisioning step is supplied as a plain function so
    /// that the scope handling, which depends on inlined runtime internals, stays inside this assembly.
    /// </remarks>
    let internal provideScoped
        (provision: 'input -> Scope -> CancellationToken -> Execution<'environment, 'error>)
        (flow: Flow<'environment, 'error, 'value>)
        : Flow<'input, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let scope = new Scope()
            let runtime = RuntimeState.current() |> RuntimeContext.withScope scope

            Platform.runScoped
                scope.Close
                cancellationToken
                (fun () ->
                    RuntimeState.withRuntime runtime (fun () ->
                        provision environment scope cancellationToken
                        |> Execution.bind (fun innerEnvironment ->
                            invoke flow innerEnvironment cancellationToken)))
                (fun cleanupError executionError exit ->
                    combineCleanup cleanupError executionError exit "Layer provisioning produced no outcome."))

    /// <summary>Defers flow construction until execution time.</summary>
    /// <param name="factory">A function that returns the flow to execute.</param>
    /// <returns>A flow that lazily evaluates the factory when executed.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.delay (fun () -> Flow.succeed 42)
    /// </code>
    /// </example>
    let delay (factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun environment ct -> invoke (factory ()) environment ct)

    /// <summary>Transforms a sequence of values into a flow and stops at the first failure.</summary>
    /// <param name="mapping">A function that maps each value to a flow.</param>
    /// <param name="values">The sequence of values to transform.</param>
    /// <returns>A flow containing a list of the successful mapped values.</returns>
    /// <example>
    /// <code>
    /// let flows = [1; 2; 3] |> Flow.traverse (fun x -> Flow.succeed (x * 2))
    /// </code>
    /// </example>
    let traverse
        (mapping: 'value -> Flow<'env, 'error, 'next>)
        (values: seq<'value>)
        : Flow<'env, 'error, 'next list> =
        Flow(fun environment ct ->
            values
            |> Seq.fold
                (fun effect value ->
                    effect
                    |> Execution.bind (fun results ->
                        invoke (mapping value) environment ct
                        |> Execution.map (fun mapped -> mapped :: results)))
                (Execution.ofValue [])
            |> Execution.map List.rev)

    // Runs `mapping` over `values` with at most `parallelism` workers. Each worker takes the next index from a shared
    // counter, so a slow item delays only its own worker. Workers are combined with sequencePar, which interrupts the
    // others as soon as one fails. `store` records each result; the counter and storage are allocated per run.
    let private runWorkers
        (parallelism: Parallelism)
        (mapping: 'value -> Flow<'env, 'error, 'next>)
        (items: 'value array)
        (store: int -> 'next -> unit)
        : Flow<'env, 'error, unit> =
        let next = ref -1L

        let worker =
            Flow(fun environment cancellationToken ->
                // A loop rather than recursion, so a worker that processes many items runs in constant memory.
                Execution.loop () (fun () ->
                    let index = int (Platform.nextId next)

                    if index >= items.Length then
                        Execution.ofValue (Platform.Break())
                    else
                        invoke (mapping items[index]) environment cancellationToken
                        |> Execution.map (fun mapped ->
                            store index mapped
                            Platform.Continue())))

        let workers = min (Parallelism.value parallelism) items.Length
        let combined = sequencePar (List.replicate workers worker)

        Flow(fun environment cancellationToken ->
            invoke combined environment cancellationToken |> Execution.map (fun (_: unit list) -> ()))

    /// <summary>Maps values to flows and runs them with bounded concurrency, returning results in input order.</summary>
    /// <remarks>
    /// At most <paramref name="parallelism" /> mappings run at once; as each finishes, its worker starts the next
    /// value. The first failure interrupts the mappings still running and waits for their cleanup before the flow
    /// fails, so no sibling is left running in the background. Size CPU-bound work with
    /// <c>Parallelism.ofProcessors</c>.
    /// </remarks>
    /// <param name="parallelism">The maximum number of mappings running at once.</param>
    /// <param name="mapping">Maps each value to a flow.</param>
    /// <param name="values">The values to map.</param>
    /// <returns>A flow containing the mapped values in the order of <paramref name="values" />.</returns>
    /// <example>
    /// <code>
    /// let! pages = urls |&gt; Flow.traversePar (Parallelism.bounded 8) fetchPage
    /// </code>
    /// </example>
    let traversePar
        (parallelism: Parallelism)
        (mapping: 'value -> Flow<'env, 'error, 'next>)
        (values: seq<'value>)
        : Flow<'env, 'error, 'next list> =
        Flow(fun environment cancellationToken ->
            let items = Array.ofSeq values
            let results = Array.zeroCreate items.Length

            invoke (runWorkers parallelism mapping items (fun index mapped -> results[index] <- mapped)) environment cancellationToken
            |> Execution.map (fun () -> List.ofArray results))

    /// <summary>Runs a flow for each value with bounded concurrency, discarding the results.</summary>
    /// <remarks>Runs like <c>traversePar</c>: at most <paramref name="parallelism" /> at once, and the first failure interrupts the rest.</remarks>
    /// <param name="parallelism">The maximum number of flows running at once.</param>
    /// <param name="action">The flow to run for each value.</param>
    /// <param name="values">The values to process.</param>
    /// <example>
    /// <code>
    /// do! files |&gt; Flow.forEachPar (Parallelism.ofProcessors id) indexFile
    /// </code>
    /// </example>
    let forEachPar
        (parallelism: Parallelism)
        (action: 'value -> Flow<'env, 'error, unit>)
        (values: seq<'value>)
        : Flow<'env, 'error, unit> =
        Flow(fun environment cancellationToken ->
            invoke (runWorkers parallelism action (Array.ofSeq values) (fun _ () -> ())) environment cancellationToken)

    /// <summary>Transforms a sequence of flows into a flow of a sequence and stops at the first failure.</summary>
    /// <param name="flows">The sequence of flows to run.</param>
    /// <returns>A flow containing a list of the successful values.</returns>
    /// <example>
    /// <code>
    /// Flow.sequence [Flow.succeed 1; Flow.succeed 2] |> Flow.run ()
    /// </code>
    /// </example>
    let sequence (flows: seq<Flow<'env, 'error, 'value>>) : Flow<'env, 'error, 'value list> =
        traverse id flows

    // -----------------------------------------------------------------------------------------
    // Execution entry points.
    //
    // Naming states when work begins: `to*` builds a description and starts nothing, `start*`
    // begins execution immediately and hands back a handle, `run` executes to completion.
    // -----------------------------------------------------------------------------------------

    /// <summary>Builds a cold async that runs the workflow when it is started.</summary>
    /// <remarks>Nothing executes until the returned async is run.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="flow">The workflow to describe.</param>
    /// <returns>A cold async that completes with the workflow exit.</returns>
    /// <example>
    /// <code>
    /// let handle = workflow |> Flow.toAsync environment
    /// </code>
    /// </example>
    let toAsync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Async<Exit<'value, 'error>> =
        flow.ToAsync(environment)

#if !FABLE_COMPILER
    /// <summary>Starts the workflow immediately and returns a task handle for its final exit.</summary>
    /// <remarks>The work is already in flight when this returns. Use <c>Flow.toAsync</c> for a cold handle.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="flow">The workflow to start.</param>
    /// <returns>A task that completes with the workflow exit.</returns>
    /// <example>
    /// <code>
    /// let running = workflow |> Flow.startTask environment
    /// </code>
    /// </example>
    let startTask (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Task<Exit<'value, 'error>> =
        flow.StartAsTask(environment)

    /// <summary>Runs the workflow and blocks until the final exit is available.</summary>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="flow">The workflow to run.</param>
    /// <returns>The final workflow exit.</returns>
    /// <example>
    /// <code>
    /// let exit = workflow |> Flow.run environment
    /// </code>
    /// </example>
    let run (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
        flow.RunSynchronously(environment)
#endif

/// <summary>Operations on a running <see cref="T:Axial.Fiber`2" />, the handle returned by <c>Flow.fork</c>.</summary>
/// <remarks>
/// Every operation returns a flow; nothing waits or interrupts until that flow runs. Reading a fiber's outcome
/// through <c>join</c>, <c>await</c>, or <c>interrupt</c> marks it observed, so a defect it died with is not also
/// reported as unobserved.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Fiber =
    /// <summary>Returns a snapshot of the fiber's diagnostic metadata.</summary>
    /// <param name="fiber">The fiber to describe.</param>
    let dump (fiber: Fiber<'error, 'value>) : FiberDump =
        FiberDump.ofMetadata fiber.Metadata

    /// <summary>Waits for a fiber and returns its value, failing the same way the fiber failed.</summary>
    /// <remarks>
    /// Joining preserves the child's error channel: a <c>Cause.Fail</c> becomes the same typed error, and
    /// interruption and defects remain interruption and defects. Use <c>await</c> to inspect the outcome instead.
    /// </remarks>
    /// <param name="fiber">The fiber to join.</param>
    /// <returns>A flow that completes with the fiber's value.</returns>
    /// <example>
    /// <code>
    /// flow {
    ///     let! fiber = Flow.fork loadProfile
    ///     let! orders = loadOrders
    ///     let! profile = Fiber.join fiber
    ///     return profile, orders
    /// }
    /// </code>
    /// </example>
    let join (fiber: Fiber<'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ ->
            fiber.MarkObserved()
            Platform.joinExitTask fiber.ExitTask)

    /// <summary>Waits for a fiber and returns its <see cref="T:Axial.Exit`2" />, never failing.</summary>
    /// <remarks>
    /// Use this when the caller decides what the fiber's outcome means, for example a cache that shares one
    /// computation between callers, or a supervisor that restarts children. Unlike <c>Deferred.await</c>, which
    /// returns the completed value, this returns the whole exit. Interrupting the awaiting flow stops the wait,
    /// not the fiber.
    /// </remarks>
    /// <param name="fiber">The fiber to wait for.</param>
    /// <returns>A flow that succeeds with the fiber's exit.</returns>
    /// <example>
    /// <code>
    /// flow {
    ///     match! Fiber.await fiber with
    ///     | Exit.Success value -> return Some value
    ///     | Exit.Failure _ -> return None
    /// }
    /// </code>
    /// </example>
    let await (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
        Flow(fun _ _ ->
            fiber.MarkObserved()
            Platform.awaitExitTaskAsSuccess fiber.ExitTask)

    /// <summary>Returns the fiber's exit if it has settled, without waiting.</summary>
    /// <remarks>A settled fiber read this way is not marked observed; use <c>await</c> to consume its outcome.</remarks>
    /// <param name="fiber">The fiber to check.</param>
    /// <returns>A flow that succeeds with <c>Some exit</c> once the fiber has settled, otherwise <c>None</c>.</returns>
    let poll (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error> option> =
        Flow(fun _ _ -> Execution.ofValue fiber.Settled)

    /// <summary>Signals a fiber to stop, waits for its cleanup, and returns its final exit.</summary>
    /// <remarks>
    /// Interruption requests cooperative cancellation through the fiber's cancellation source. A fiber that had
    /// already settled keeps its original exit.
    /// </remarks>
    /// <param name="fiber">The fiber to interrupt.</param>
    /// <returns>A flow that completes with the fiber's final outcome after interruption.</returns>
    let interrupt (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
        Flow(fun _ _ ->
            fiber.MarkObserved()
            fiber.InterruptSource.Cancel()
            Platform.awaitExitTaskAsSuccess fiber.ExitTask)
