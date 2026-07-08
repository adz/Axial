namespace Axial.Flow

open System
open System.Threading
open System.Threading.Tasks

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

    let internal runSynchronouslyInternal
        (environment: 'env)
        (timeout: int option)
        (cancellationToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        : Exit<'value, 'error> =
        let task = toTaskInternal environment cancellationToken flow

        match timeout with
        | None ->
            task.GetAwaiter().GetResult()
        | Some millisecondsTimeout ->
            if task.Wait(millisecondsTimeout) then
                task.GetAwaiter().GetResult()
            else
                raise (TimeoutException("The flow did not complete before the timeout."))
    #endif

#if !FABLE_COMPILER
    /// <summary>Registers an asynchronous finalizer with the current runtime scope.</summary>
    /// <param name="finalizer">The finalizer to run when the current scope closes.</param>
    /// <returns>A flow that registers the finalizer.</returns>
    /// <remarks>
    /// Use this when a resource acquired by a subflow should live until the surrounding
    /// runtime or layer scope closes, rather than only until the current expression ends.
    /// </remarks>
    let addFinalizer
        (finalizer: CancellationToken -> Task)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddFinalizer finalizer
            Execution.ofValue ())

    /// <summary>Registers a disposable resource with the current runtime scope.</summary>
    /// <param name="resource">The disposable resource to close when the current scope closes.</param>
    /// <returns>A flow that registers the resource.</returns>
    let addDisposable
        (resource: IDisposable)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddDisposable resource
            Execution.ofValue ())

    /// <summary>Registers an asynchronously disposable resource with the current runtime scope.</summary>
    /// <param name="resource">The async disposable resource to close when the current scope closes.</param>
    /// <returns>A flow that registers the resource.</returns>
    let addAsyncDisposable
        (resource: IAsyncDisposable)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            RuntimeState.current().Scope.AddAsyncDisposable resource
            Execution.ofValue ())

    /// <summary>Acquires a resource and registers its release with the current runtime scope.</summary>
    /// <param name="acquire">The flow that acquires the resource.</param>
    /// <param name="release">The release action to run when the current scope closes.</param>
    /// <returns>A flow that succeeds with the acquired resource.</returns>
    /// <remarks>
    /// The resource is not released when this expression finishes. It is released when the
    /// surrounding runtime scope closes, which makes it suitable for resources acquired by
    /// subflows and then shared by later work in the same execution boundary.
    /// </remarks>
    let acquireRelease
        (acquire: Flow<'env, 'error, 'resource>)
        (release: 'resource -> CancellationToken -> Task)
        : Flow<'env, 'error, 'resource> =
        Flow(fun environment cancellationToken ->
            invoke acquire environment cancellationToken
            |> Execution.bind (fun resource ->
                RuntimeState.current().Scope.AddFinalizer(fun ct -> release resource ct)
                Execution.ofValue resource))

    /// <summary>Acquires a resource, uses it, and always runs the release action.</summary>
    /// <param name="acquire">The flow that acquires the resource.</param>
    /// <param name="release">The release action to run after the resource is used.</param>
    /// <param name="useResource">The flow that uses the acquired resource.</param>
    /// <returns>A flow that releases the resource after use, including failure paths.</returns>
    /// <remarks>
    /// Use this for lexical acquire/use/release. For resources that should live until the
    /// surrounding scope closes, use <see cref="M:Axial.Flow.acquireRelease" />.
    /// </remarks>
    let acquireReleaseWith
        (acquire: Flow<'env, 'error, 'resource>)
        (release: 'resource -> CancellationToken -> Task)
        (useResource: 'resource -> Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    let! acquireExit = invoke acquire environment cancellationToken

                    match acquireExit with
                    | Exit.Failure cause ->
                        return Exit.Failure cause
                    | Exit.Success resource ->
                        let! useExit =
                            task {
                                try
                                    return! invoke (useResource resource) environment cancellationToken |> _.AsTask()
                                with error ->
                                    return Exit.Failure (Execution.causeOfException error)
                            }

                        try
                            do! release resource cancellationToken
                            return useExit
                        with error ->
                            match useExit with
                            | Exit.Failure cause ->
                                return Exit.Failure (Cause.thenCause cause (Execution.causeOfException error))
                            | Exit.Success _ ->
                                return Exit.Failure (Execution.causeOfException error)
                }))
#endif

    /// <summary>Creates a flow from a raw async operation.</summary>
    /// <remarks>Thrown exceptions are recorded as defects (<c>Cause.Die</c>), while cancellation is recorded as interruption. Use <c>attemptAsync</c> when expected exceptions should enter the typed error channel.</remarks>
    /// <platforms>Fable compatible</platforms>
    let fromAsync (operation: Async<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            Platform.tryExecution
                (fun () -> operation |> Platform.executionOfAsyncUnguarded cancellationToken Exit.Success)
                (fun error -> Platform.ofExit (Exit.Failure(Execution.causeOfException error))))

    /// <summary>Creates a flow from an async operation and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Successful completion returns <c>Exit.Success</c>. <c>OperationCanceledException</c> returns <c>Cause.Interrupt</c>. Other exceptions return <c>Cause.Fail exn</c>.</remarks>
    /// <platforms>Fable compatible</platforms>
    let attemptAsync (operation: Async<'value>) : Flow<'env, exn, 'value> =
        Flow(fun _ cancellationToken ->
            Platform.tryExecution
                (fun () -> operation |> Platform.executionOfAsyncUnguarded cancellationToken Exit.Success)
                (fun error ->
                    match error with
                    | :? OperationCanceledException -> Platform.ofExit (Exit.Failure Cause.Interrupt)
                    | error -> Platform.ofExit (Exit.Failure(Cause.Fail error))))

#if !FABLE_COMPILER
    /// <summary>Creates a flow from a raw task operation.</summary>
    /// <remarks>Thrown exceptions are recorded as defects (<c>Cause.Die</c>), while cancellation is recorded as interruption. Use <c>attemptTask</c> when expected exceptions should enter the typed error channel.</remarks>
    /// <platforms>.NET only</platforms>
    let fromTask (operation: Task<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    try
                        let! value = operation
                        return Exit.Success value
                    with error ->
                        return Exit.Failure (Execution.causeOfException error)
                }))

    /// <summary>Creates a flow from a task operation and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Successful completion returns <c>Exit.Success</c>. <c>OperationCanceledException</c> returns <c>Cause.Interrupt</c>. Other exceptions return <c>Cause.Fail exn</c>.</remarks>
    /// <platforms>.NET only</platforms>
    let attemptTask (operation: Task<'value>) : Flow<'env, exn, 'value> =
        Flow(fun _ _ ->
            ValueTask<Exit<'value, exn>>(
                task {
                    try
                        let! value = operation
                        return Exit.Success value
                    with
                    | :? OperationCanceledException ->
                        return Exit.Failure Cause.Interrupt
                    | error ->
                        return Exit.Failure (Cause.Fail error)
                }))

    /// <summary>Creates a flow from a raw value task operation.</summary>
    /// <remarks>Thrown exceptions are recorded as defects (<c>Cause.Die</c>), while cancellation is recorded as interruption. Use <c>attemptValueTask</c> when expected exceptions should enter the typed error channel.</remarks>
    /// <platforms>.NET only</platforms>
    let fromValueTask (operation: ValueTask<'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    try
                        let! value = operation.AsTask()
                        return Exit.Success value
                    with error ->
                        return Exit.Failure (Execution.causeOfException error)
                }))

    /// <summary>Creates a flow from a value task operation and treats thrown exceptions as recoverable typed errors.</summary>
    /// <remarks>Successful completion returns <c>Exit.Success</c>. <c>OperationCanceledException</c> returns <c>Cause.Interrupt</c>. Other exceptions return <c>Cause.Fail exn</c>.</remarks>
    /// <platforms>.NET only</platforms>
    let attemptValueTask (operation: ValueTask<'value>) : Flow<'env, exn, 'value> =
        Flow(fun _ _ ->
            ValueTask<Exit<'value, exn>>(
                task {
                    try
                        let! value = operation.AsTask()
                        return Exit.Success value
                    with
                    | :? OperationCanceledException ->
                        return Exit.Failure Cause.Interrupt
                    | error ->
                        return Exit.Failure (Cause.Fail error)
                }))
#endif

    /// <summary>Creates a successful synchronous flow.</summary>
    /// <param name="value">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    let ok (value: 'value) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofValue value)

    /// <summary>Alias for <c>ok</c> that reads well in some call sites.</summary>
    /// <param name="value">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 42
    /// let result = flow.RunSynchronously(())
    /// // result = Success 42
    /// </code>
    /// </example>
    let succeed (value: 'value) : Flow<'env, 'error, 'value> =
        ok value

    /// <summary>Alias for <c>ok</c> that reads well in some call sites.</summary>
    /// <param name="item">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.value "constant"
    /// </code>
    /// </example>
    let value (item: 'value) : Flow<'env, 'error, 'value> =
        succeed item

    /// <summary>Creates a failing synchronous flow.</summary>
    /// <param name="failure">The error value to wrap in a failing flow.</param>
    /// <returns>A flow that always fails with the provided error.</returns>
    let error (failure: 'error) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofError failure)

    /// <summary>Alias for <c>error</c> that reads well in some call sites.</summary>
    /// <param name="failure">The error value to wrap in a failing flow.</param>
    /// <returns>A flow that always fails with the provided error.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error"
    /// let result = flow.RunSynchronously(())
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
    /// let res = Ok "success"
    /// let flow = Flow.fromResult res
    /// </code>
    /// </example>
    let fromResult (result: Result<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Execution.ofResult result)

    /// <summary>Runs an environment-aware policy against an input value inside a workflow.</summary>
    /// <param name="policy">The policy to run.</param>
    /// <param name="input">The input value supplied to the policy.</param>
    /// <returns>A flow that succeeds or fails with the policy result.</returns>
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

    /// <summary>Installs a runtime annotation sink for integration packages.</summary>
    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    let withAnnotationSink
        (sink: string -> string -> unit)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withAnnotationSink sink) flow

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
            currentRuntime.AnnotationSink name value
            let runtime = currentRuntime |> RuntimeContext.withAnnotation name value

            RuntimeState.withRuntime runtime (fun () -> invoke flow environment cancellationToken))

    /// <summary>Adds the standard <c>trace_id</c> runtime annotation for the duration of the supplied flow.</summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that runs with the supplied trace id in the ambient runtime context.</returns>
    let traceId
        (traceId: string)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        annotate "trace_id" traceId flow

    /// <summary>Runtime helpers for execution-time concerns like cancellation, scope, timeout, retry, and cleanup.</summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        /// <summary>Reads the current runtime cancellation token.</summary>
        /// <returns>A flow that succeeds with the token supplied at the workflow execution boundary.</returns>
        let cancellationToken<'env, 'error> : Flow<'env, 'error, CancellationToken> =
            Flow(fun _ cancellationToken -> Execution.ofValue cancellationToken)

        /// <summary>Catches <see cref="OperationCanceledException" /> raised by a flow and converts it into a typed error.</summary>
        /// <param name="handler">Maps the cancellation exception into the workflow error type.</param>
        /// <param name="flow">The source flow.</param>
        /// <returns>A flow that turns thrown cancellation into <c>Cause.Fail</c>.</returns>
        /// <remarks>
        /// This handles cancellation exceptions thrown during execution. A flow that has already returned
        /// <c>Cause.Interrupt</c> remains interrupted.
        /// </remarks>
        let catchCancellation
            (handler: OperationCanceledException -> 'error)
            (flow: Flow<'env, 'error, 'value>)
            : Flow<'env, 'error, 'value> =
            Flow(fun environment cancellationToken ->
                Platform.tryExecution
                    (fun () -> invoke flow environment cancellationToken)
                    (fun error ->
                        match error with
                        | :? OperationCanceledException as error -> Platform.ofExit (Exit.Failure(Cause.Fail(handler error)))
                        | error -> raise error))

        /// <summary>Returns a typed error immediately when the runtime token is already canceled.</summary>
        /// <param name="canceledError">The error to return when cancellation has been requested.</param>
        /// <returns>A flow that succeeds with unit when cancellation has not been requested.</returns>
        let ensureNotCanceled<'env, 'error> (canceledError: 'error) : Flow<'env, 'error, unit> =
            Flow(fun _ cancellationToken ->
                if cancellationToken.IsCancellationRequested then
                    Execution.ofError canceledError
                else
                    Execution.ofValue ())

        /// <summary>Suspends the flow for the specified duration, observing cancellation.</summary>
        /// <param name="delay">The duration to sleep.</param>
        /// <returns>A flow that completes after the specified delay.</returns>
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

        /// <summary>Reads the current runtime trace id annotation if one is present.</summary>
        /// <returns>A flow that succeeds with the ambient <c>trace_id</c> value, if present.</returns>
        let traceId<'env, 'error> : Flow<'env, 'error, string option> =
            Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Annotations |> Map.tryFind "trace_id"))

        /// <summary>Fails with the supplied typed error when the flow does not complete before the timeout.</summary>
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
                    (fun () -> Platform.ofExit (Exit.Success value)))

        /// <summary>Alias for <c>timeout</c> that emphasizes typed failure on timeout.</summary>
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
                    (fun () -> invoke (fallback ()) environment cancellationToken))

        /// <summary>Retries typed failures according to the specified policy.</summary>
        /// <param name="policy">The retry policy.</param>
        /// <param name="flow">The source flow.</param>
        /// <returns>A flow that retries <c>Cause.Fail</c> outcomes when the policy allows it.</returns>
        /// <remarks>Defects and interruptions are not retried.</remarks>
        let retry
            (policy: RetryPolicy<'error>)
            (flow: Flow<'env, 'error, 'value>)
            : Flow<'env, 'error, 'value> =
            if policy.MaxAttempts < 1 then
                invalidArg (nameof policy.MaxAttempts) "RetryPolicy.MaxAttempts must be at least 1."

            let rec loop attempt =
                Flow(fun environment cancellationToken ->
                    invoke flow environment cancellationToken
                    |> Execution.fold
                        Execution.ofValue
                        (fun cause ->
                            match cause with
                            | Cause.Fail error when attempt < policy.MaxAttempts && policy.ShouldRetry error ->
                                let delay = policy.Delay attempt

                                Platform.delayThenExecution delay cancellationToken (fun () ->
                                    invoke (loop (attempt + 1)) environment cancellationToken)
                            | _ ->
                                Execution.ofCause cause))

            loop 1

    /// <summary>Starts a flow in a new fiber without waiting for it to complete.</summary>
    /// <remarks>
    /// Forking turns a cold flow description into hot child work and returns a handle
    /// that can later be joined or interrupted. Prefer <c>zipPar</c> or <c>race</c>
    /// when the caller only needs a simple parallel composition.
    /// </remarks>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:Axial.Fiber`2" /> handle.</returns>
    let fork (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            let parentRuntime = RuntimeState.current()

            let metadata: FiberMetadata =
                {
                    Id = FiberId.next ()
                    ParentId = Some parentRuntime.FiberId
                    StartedAt = DateTimeOffset.UtcNow
                    Status = FiberStatus.Running
                }

            let childRuntime = parentRuntime |> RuntimeContext.withFiberId metadata.Id

            let cts, exitTask =
                Platform.startFiber
                    cancellationToken
                    (fun status -> metadata.Status <- status)
                    (fun childToken ->
                        RuntimeState.withRuntime childRuntime (fun () -> invoke flow environment childToken))

            let fiber =
                {
                    Metadata = metadata
                    ExitTask = exitTask
                    InterruptSource = cts
                }

            Execution.ofValue fiber)

    /// <summary>Waits for a fiber to complete and returns its successful value or typed failure.</summary>
    /// <remarks>
    /// Joining preserves the child workflow's error channel. If the child failed with
    /// <c>Cause.Fail</c>, the joined flow fails with the same typed error; interruption
    /// and defects remain interruption and defects.
    /// </remarks>
    /// <param name="fiber">The fiber to join.</param>
    /// <returns>A flow that completes with the fiber's outcome.</returns>
    let join (fiber: Fiber<'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> Platform.joinExitTask fiber.ExitTask)

    /// <summary>Signals a fiber to stop and waits for it to finish its cleanup.</summary>
    /// <remarks>
    /// Interruption requests cooperative cancellation through the fiber's cancellation
    /// source and then waits for the child operation to report its final
    /// <see cref="T:Axial.Exit`2" />.
    /// </remarks>
    /// <param name="fiber">The fiber to interrupt.</param>
    /// <returns>A flow that completes with the fiber's final outcome after interruption.</returns>
    let interrupt (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
        Flow(fun _ _ ->
            fiber.InterruptSource.Cancel()
            Platform.awaitExitTaskAsSuccess fiber.ExitTask)

    /// <summary>Combines two flows into a tuple of their values, running them concurrently.</summary>
    /// <remarks>
    /// If either flow fails, the other is interrupted immediately.
    /// </remarks>
    /// <param name="left">The first flow to combine.</param>
    /// <param name="right">The second flow to combine.</param>
    /// <returns>A flow that returns a tuple of both successful values.</returns>
    /// <example>
    /// <code>
    /// let combined = Flow.zipPar flow1 flow2
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

    /// <summary>Runs two flows concurrently and returns the result of the first one to complete.</summary>
    /// <remarks>
    /// The "loser" flow is interrupted immediately.
    /// </remarks>
    /// <param name="left">The first flow to run.</param>
    /// <param name="right">The second flow to run.</param>
    /// <returns>A flow containing the result of the first flow to complete.</returns>
    /// <example>
    /// <code>
    /// let fastOrSlow = Flow.race fastFlow slowFlow
    /// </code>
    /// </example>
    let race
        (left: Flow<'env, 'error, 'value>)
        (right: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            Platform.raceExecution (invoke left environment) (invoke right environment) cancellationToken)

    /// <summary>Lifts an option into a synchronous flow with the supplied error.</summary>
    /// <param name="error">The error to return if the option is <c>None</c>.</param>
    /// <param name="value">The option to lift.</param>
    /// <returns>A flow that succeeds with the option's value or fails with the provided error.</returns>
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
    /// <returns>A <see cref="T:Axial.Flow`3" /> that succeeds with the option's value or fails with the provided error.</returns>
    let fromValueOption (error: 'error) (value: 'value voption) : Flow<'env, 'error, 'value> =
        value
        |> OptionFlow.toResultValueOption error
        |> fromResult

    /// <summary>Turns a pure validation result into a synchronous flow with environment-provided failure.</summary>
    /// <remarks>
    /// This helper bridges the gap between pure validation (which often uses <see cref="T:System.Result`2" /> or <see cref="T:Axial.Check`1" />)
    /// and the <see cref="T:Axial.Flow`3" /> environment model. If the result is an error, the provided <paramref name="errorFlow" />
    /// is executed to produce the final application error.
    /// </remarks>
    /// <param name="errorFlow">A flow that reads the environment to produce an error value.</param>
    /// <param name="result">The pure result to bridge.</param>
    /// <returns>A <see cref="T:Axial.Flow`3" /> that mirrors the success of the result or fails with the outcome of the error flow.</returns>
    /// <example>
    /// <code>
    /// let result = Result.Error ()
    /// let flow = Flow.orElseFlow (Flow.read (fun env -> "error")) result
    /// </code>
    /// </example>
    let orElseFlow
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
    /// Use this when the next step genuinely needs the whole environment value, for example when
    /// passing a request context to another helper. For a single dependency or configuration value,
    /// prefer <c>Flow.read</c>; it keeps the dependency local and makes the workflow easier to scan.
    /// </remarks>
    /// <returns>A <see cref="T:Axial.Flow`3" /> whose successful value is the current environment.</returns>
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
    /// <returns>A <see cref="T:Axial.Flow`3" /> containing the projected value.</returns>
    /// <example>
    /// <code>
    /// let myFlow = Flow.read (fun env -> env)
    /// </code>
    /// </example>
    let read (projection: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow(fun environment _ -> Execution.ofValue (projection environment))

    /// <summary>Transforms the successful value of a flow.</summary>
    /// <remarks>
    /// If the source <paramref name="flow" /> fails, the <paramref name="mapper" /> is not executed.
    /// The original failure cause is preserved, including typed failures, interruption, and defects.
    /// Use <c>map</c> for pure value transformations after an effect has succeeded.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the successful value.</param>
    /// <param name="flow">The source flow of type <see cref="T:Axial.Flow`3" /> to transform.</param>
    /// <returns>A new <see cref="T:Axial.Flow`3" /> with the transformed success value of type <c>'next</c>.</returns>
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
    /// This is the flatmap operation for <see cref="T:Axial.Flow`3" />. The continuation only runs
    /// when the source flow succeeds, and it receives the successful value. Use <c>bind</c> when the
    /// next effect depends on the previous result; use <c>map</c> when the next step is pure.
    /// </remarks>
    /// <param name="binder">A function that takes the successful value and returns a new flow.</param>
    /// <param name="flow">The source flow to sequence.</param>
    /// <returns>A <see cref="T:Axial.Flow`3" /> representing the combined workflow.</returns>
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
    /// <returns>A <see cref="T:Axial.Flow`3" /> that preserves the original success value after the side effect.</returns>
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
    /// <returns>A <see cref="T:Axial.Flow`3" /> that preserves the original error after the side effect.</returns>
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
    /// <returns>A <see cref="T:Axial.Flow`3" /> with the transformed error type.</returns>
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

    /// <summary>Maps both the successful value and the failure cause of a synchronous flow.</summary>
    /// <param name="onSuccess">The function to transform the success value.</param>
    /// <param name="onFailure">The function to transform the failure cause.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A new <see cref="T:Axial.Flow`3" /> with transformed success and error types.</returns>
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
    /// Existing typed failures and interruptions are preserved. Compound causes are preserved unchanged.
    /// </remarks>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source flow of type <see cref="T:Axial.Flow`3" /> to monitor.</param>
    /// <returns>A <see cref="T:Axial.Flow`3" /> that converts recoverable exceptions into typed errors.</returns>
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
                (fun error -> Platform.ofExit (Exit.Failure(Cause.Fail(handler error)))))

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
    /// let flow = Flow.zip (Flow.succeed 1) (Flow.succeed 2)
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

    /// <summary>Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.</summary>
    /// <remarks>
    /// This is the provisioning boundary for explicit services. It creates a fresh scope, builds the
    /// supplied layer inside that scope, runs the downstream flow with the built environment, and
    /// finalizes all acquired resources when the downstream flow completes or fails.
    /// </remarks>
    /// <param name="layer">The layer that builds the downstream environment.</param>
    /// <param name="flow">The flow to run with the provided environment.</param>
    /// <returns>A flow that requires only the input environment of the layer.</returns>
    let provide
        (layer: Layer<'input, 'error, 'environment>)
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
                        Layer.invoke layer environment scope cancellationToken
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

    /// <summary>Transforms a sequence of flows into a flow of a sequence and stops at the first failure.</summary>
    /// <param name="flows">The sequence of flows to run.</param>
    /// <returns>A flow containing a list of the successful values.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.sequence [Flow.succeed 1; Flow.succeed 2]
    /// </code>
    /// </example>
    let sequence (flows: seq<Flow<'env, 'error, 'value>>) : Flow<'env, 'error, 'value list> =
        traverse id flows
