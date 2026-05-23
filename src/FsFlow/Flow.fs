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
        FlowInternal.invoke flow environment cancellationToken

    let inline private runEffect
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        : Effect<'value, 'error> =
        RuntimeState.withRuntime RuntimeContext.live (fun () ->
            #if FABLE_COMPILER
            async {
                try
                    return! invoke flow environment cancellationToken
                with error ->
                    return Exit.Failure (EffectFlow.causeOfException error)
            }
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    try
                        let! exit = invoke flow environment cancellationToken
                        return exit
                    with error ->
                        return Exit.Failure (EffectFlow.causeOfException error)
                })
            #endif
        )

    /// <summary>Creates a flow from an execution outcome.</summary>
    let ofExit (exit: Exit<'value, 'error>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofExit exit)

    /// <summary>Internal alias for invoke used by orchestration helpers.</summary>
    let internal runFullInternal = invoke

    /// <summary>Executes a flow with an explicit cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="cancellationToken">The token used to signal cancellation.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>An effect that represents the asynchronous execution outcome.</returns>
    /// <remarks>Uncaught exceptions become <c>Cause.Die</c>; cancellation becomes <c>Cause.Interrupt</c>.</remarks>
    let runFull (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Effect<'value, 'error> =
        runEffect environment cancellationToken flow

    /// <summary>Executes a flow with an explicit cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="cancellationToken">The token used to signal cancellation.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>An effect that represents the asynchronous execution outcome.</returns>
    let runWithToken = runFull

    /// <summary>Executes a flow with the provided environment and the default cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>An effect that represents the asynchronous execution outcome.</returns>
    /// <remarks>Uncaught exceptions become <c>Cause.Die</c>; cancellation becomes <c>Cause.Interrupt</c>.</remarks>
    /// <example>
    /// <code>
    /// let flow = Flow.read (fun env -> $"Hello, {env}!")
    /// let result = Flow.run "World" flow
    /// // result = Effect that resolves to Success "Hello, World!" on both .NET and Fable
    /// </code>
    /// </example>
    let run (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Effect<'value, 'error> =
        runEffect environment CancellationToken.None flow

    /// <summary>Executes a flow and returns an async that resolves to the final exit outcome, observing the ambient cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>An async that completes with the fiber's final outcome.</returns>
    let toAsync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Async<Exit<'value, 'error>> =
        async {
            let! ct = Async.CancellationToken
            #if FABLE_COMPILER
            return! runFull environment ct flow
            #else
            return! (runFull environment ct flow).AsTask() |> Async.AwaitTask
            #endif
        }

    /// <summary>Executes a flow and returns an async that resolves to a standard result, observing the ambient cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>An async that completes with a <see cref="T:System.Result`2" /> representing the successful value or domain failure.</returns>
    /// <remarks>Interruption signals and defects are raised as exceptions in the caller's context.</remarks>
    let toAsyncResult (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Async<Result<'value, 'error>> =
        async {
            let! exit = toAsync environment flow
            return Exit.toResult exit
        }

    #if !FABLE_COMPILER
    /// <summary>Executes a flow and returns a task that resolves to the final exit outcome.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>A task that completes with the fiber's final outcome.</returns>
    let toTask (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Task<Exit<'value, 'error>> =
        (run environment flow).AsTask()

    /// <summary>Executes a flow and returns a task that resolves to the final exit outcome with an explicit cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="cancellationToken">The token used to signal cancellation.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>A task that completes with the fiber's final outcome.</returns>
    let toTaskWithToken (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Task<Exit<'value, 'error>> =
        (runFull environment cancellationToken flow).AsTask()

    /// <summary>Executes a flow and returns a task that resolves to a standard result.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>A task that completes with a <see cref="T:System.Result`2" /> representing the successful value or domain failure.</returns>
    /// <remarks>Interruption signals and defects are raised as exceptions in the caller's context.</remarks>
    let toTaskResult (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Task<Result<'value, 'error>> =
        task {
            let! exit = toTask environment flow
            return Exit.toResult exit
        }

    /// <summary>Executes a flow and returns a task that resolves to a standard result with an explicit cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="cancellationToken">The token used to signal cancellation.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>A task that completes with a <see cref="T:System.Result`2" /> representing the successful value or domain failure.</returns>
    /// <remarks>Interruption signals and defects are raised as exceptions in the caller's context.</remarks>
    let toTaskResultWithToken (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Task<Result<'value, 'error>> =
        task {
            let! exit = toTaskWithToken environment cancellationToken flow
            return Exit.toResult exit
        }

    /// <summary>Executes a flow and returns a value task that resolves to a standard result.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>A value task that completes with a <see cref="T:System.Result`2" /> representing the successful value or domain failure.</returns>
    /// <remarks>Interruption signals and defects are raised as exceptions in the caller's context.</remarks>
    let toValueTaskResult (environment: 'env) (flow: Flow<'env, 'error, 'value>) : ValueTask<Result<'value, 'error>> =
        ValueTask<Result<'value, 'error>>(
            task {
                let! exit = run environment flow
                return Exit.toResult exit
            })

    /// <summary>Executes a flow and returns a value task that resolves to a standard result with an explicit cancellation token.</summary>
    /// <param name="environment">The environment required by the flow.</param>
    /// <param name="cancellationToken">The token used to signal cancellation.</param>
    /// <param name="flow">The workflow to execute.</param>
    /// <returns>A value task that completes with a <see cref="T:System.Result`2" /> representing the successful value or domain failure.</returns>
    /// <remarks>Interruption signals and defects are raised as exceptions in the caller's context.</remarks>
    let toValueTaskResultWithToken (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : ValueTask<Result<'value, 'error>> =
        ValueTask<Result<'value, 'error>>(
            task {
                let! exit = runFull environment cancellationToken flow
                return Exit.toResult exit
            })
    #endif

    /// <summary>Creates a successful synchronous flow.</summary>
    /// <param name="value">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    let ok (value: 'value) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofValue value)

    /// <summary>Alias for <c>ok</c> that reads well in some call sites.</summary>
    /// <param name="value">The value to wrap in a successful flow.</param>
    /// <returns>A flow that always succeeds with the provided value.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.succeed 42
    /// let result = Flow.run () flow
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
        Flow(fun _ _ -> EffectFlow.ofError failure)

    /// <summary>Alias for <c>error</c> that reads well in some call sites.</summary>
    /// <param name="failure">The error value to wrap in a failing flow.</param>
    /// <returns>A flow that always fails with the provided error.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.fail "error"
    /// let result = Flow.run () flow
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
        Flow(fun _ _ -> EffectFlow.ofDie exn)

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
        Flow(fun _ _ -> EffectFlow.ofResult result)

    let inline private withRuntime
        (mapper: RuntimeContext -> RuntimeContext)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let runtime = mapper (RuntimeState.current())

            #if FABLE_COMPILER
            async {
                return!
                    RuntimeState.withRuntime runtime (fun () ->
                        invoke flow environment cancellationToken)
            }
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    return!
                        RuntimeState.withRuntime runtime (fun () ->
                            invoke flow environment cancellationToken |> _.AsTask())
                })
            #endif
        )

    /// <summary>Overrides the ambient clock for the duration of the supplied flow.</summary>
    let withClock (clock: IClock) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withClock clock) flow

    /// <summary>Overrides the ambient logger for the duration of the supplied flow.</summary>
    let withLog (log: ILog) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withLog log) flow

    /// <summary>Overrides the ambient random-number generator for the duration of the supplied flow.</summary>
    let withRandom (random: IRandom) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withRandom random) flow

    /// <summary>Overrides the ambient GUID generator for the duration of the supplied flow.</summary>
    let withGuid (guid: IGuid) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withGuid guid) flow

    /// <summary>Overrides the ambient environment-variable provider for the duration of the supplied flow.</summary>
    let withEnvironmentVariables
        (environmentVariables: IEnvironmentVariables)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime (RuntimeContext.withEnvironmentVariables environmentVariables) flow

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

            #if FABLE_COMPILER
            async {
                return!
                    RuntimeState.withRuntime runtime (fun () ->
                        invoke flow environment cancellationToken)
            }
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    return!
                        RuntimeState.withRuntime runtime (fun () ->
                            invoke flow environment cancellationToken |> _.AsTask())
                })
            #endif
        )

    /// <summary>Adds the standard <c>trace_id</c> runtime annotation for the duration of the supplied flow.</summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that runs with the supplied trace id in the ambient runtime context.</returns>
    let traceId
        (traceId: string)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        annotate "trace_id" traceId flow

    /// <summary>Runtime helpers for operational concerns like logging, timeout, retry, and cleanup.</summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        /// <summary>Reads the current runtime cancellation token.</summary>
        /// <returns>A flow that succeeds with the token supplied to <see cref="runFull" /> or <see cref="runWithToken" />.</returns>
        let cancellationToken<'env, 'error> : Flow<'env, 'error, CancellationToken> =
            Flow(fun _ cancellationToken -> EffectFlow.ofValue cancellationToken)

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
                #if FABLE_COMPILER
                async {
                    try
                        return! invoke flow environment cancellationToken
                    with :? OperationCanceledException as error ->
                        return Exit.Failure (Cause.Fail (handler error))
                }
                #else
                ValueTask<Exit<'value, 'error>>(
                    task {
                        try
                            return! invoke flow environment cancellationToken
                        with :? OperationCanceledException as error ->
                            return Exit.Failure (Cause.Fail (handler error))
                    })
                #endif
            )

        /// <summary>Returns a typed error immediately when the runtime token is already canceled.</summary>
        /// <param name="canceledError">The error to return when cancellation has been requested.</param>
        /// <returns>A flow that succeeds with unit when cancellation has not been requested.</returns>
        let ensureNotCanceled<'env, 'error> (canceledError: 'error) : Flow<'env, 'error, unit> =
            Flow(fun _ cancellationToken ->
                if cancellationToken.IsCancellationRequested then
                    EffectFlow.ofError canceledError
                else
                    EffectFlow.ofValue ())

        /// <summary>Suspends the flow for the specified duration, observing cancellation.</summary>
        /// <param name="delay">The duration to sleep.</param>
        /// <returns>A flow that completes after the specified delay.</returns>
        let sleep (delay: TimeSpan) : Flow<'env, 'error, unit> =
            Flow(fun _ cancellationToken ->
                #if FABLE_COMPILER
                async {
                    try
                        do! Async.Sleep(int delay.TotalMilliseconds)
                        return Exit.Success ()
                    with :? OperationCanceledException ->
                        return Exit.Failure Cause.Interrupt
                }
                #else
                ValueTask<Exit<unit, 'error>>(
                    task {
                        try
                            do! Task.Delay(delay, cancellationToken)
                            return Exit.Success ()
                        with :? OperationCanceledException ->
                            return Exit.Failure Cause.Interrupt
                    })
                #endif
            )

        /// <summary>Reads the ambient UTC clock owned by the runtime.</summary>
        /// <returns>A flow that returns the current UTC time.</returns>
        let now : Flow<'env, 'error, DateTimeOffset> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().Clock.UtcNow()))

        /// <summary>Writes a message through the ambient runtime logger.</summary>
        /// <param name="message">The message to log.</param>
        /// <returns>A flow that logs the message and returns unit.</returns>
        let log (message: string) : Flow<'env, 'error, unit> =
            Flow(fun _ _ ->
                RuntimeState.current().Log.Info message
                EffectFlow.ofValue ())

        /// <summary>Creates a new GUID through the ambient runtime GUID generator.</summary>
        /// <returns>A flow that returns a fresh GUID.</returns>
        let newGuid : Flow<'env, 'error, Guid> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().Guid.NewGuid()))

        /// <summary>Creates a random integer through the ambient runtime random generator.</summary>
        /// <param name="minInclusive">The inclusive lower bound.</param>
        /// <param name="maxExclusive">The exclusive upper bound.</param>
        /// <returns>A flow that returns a random integer in the specified range.</returns>
        let nextInt (minInclusive: int) (maxExclusive: int) : Flow<'env, 'error, int> =
            Flow(fun _ _ ->
                EffectFlow.ofValue (RuntimeState.current().Random.NextInt minInclusive maxExclusive))

        /// <summary>Reads an environment variable from the ambient runtime environment provider.</summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>A flow that returns the variable value if it exists, or None.</returns>
        let tryGetEnvironmentVariable (name: string) : Flow<'env, 'error, string option> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().EnvironmentVariables.TryGet name))

        /// <summary>Reads the current runtime annotations.</summary>
        /// <returns>A flow that succeeds with the ambient annotation map.</returns>
        let annotations<'env, 'error> : Flow<'env, 'error, Map<string, string>> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().Annotations))

        /// <summary>Reads the current runtime trace id annotation if one is present.</summary>
        /// <returns>A flow that succeeds with the ambient <c>trace_id</c> value, if present.</returns>
        let traceId<'env, 'error> : Flow<'env, 'error, string option> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().Annotations |> Map.tryFind "trace_id"))

#if !FABLE_COMPILER
        /// <summary>Acquires a resource, uses it, and always runs the release action.</summary>
        /// <param name="acquire">The flow that acquires the resource.</param>
        /// <param name="release">The task-based release action.</param>
        /// <param name="useResource">The flow that uses the acquired resource.</param>
        /// <returns>A flow that releases the resource after use, including failure paths.</returns>
        let useWithAcquireRelease
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
                            try
                                let! exit = invoke (useResource resource) environment cancellationToken
                                do! release resource cancellationToken
                                return exit
                            with error ->
                                do! release resource cancellationToken
                                return Exit.Failure (EffectFlow.causeOfException error)
                    }))
#endif

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
                #if FABLE_COMPILER
                async {
                    try
                        let! child =
                            Async.StartChild(
                                invoke flow environment cancellationToken,
                                millisecondsTimeout = int after.TotalMilliseconds
                            )

                        return! child
                    with :? TimeoutException ->
                        return Exit.Failure (Cause.Fail timeoutError)
                }
                #else
                ValueTask<Exit<'value, 'error>>(
                    task {
                        let operation = invoke flow environment cancellationToken |> _.AsTask()
                        let timeoutTask = Task.Delay after
                        let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                        if obj.ReferenceEquals(completed, timeoutTask) then
                            return Exit.Failure (Cause.Fail timeoutError)
                        else
                            return! operation
                    })
                #endif
            )

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
                #if FABLE_COMPILER
                async {
                    try
                        let! child =
                            Async.StartChild(
                                invoke flow environment cancellationToken,
                                millisecondsTimeout = int after.TotalMilliseconds
                            )

                        return! child
                    with :? TimeoutException ->
                        return Exit.Success value
                }
                #else
                ValueTask<Exit<'value, 'error>>(
                    task {
                        let operation = invoke flow environment cancellationToken |> _.AsTask()
                        let timeoutTask = Task.Delay after
                        let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                        if obj.ReferenceEquals(completed, timeoutTask) then
                            return Exit.Success value
                        else
                            return! operation
                    })
                #endif
            )

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
                #if FABLE_COMPILER
                async {
                    try
                        let! child =
                            Async.StartChild(
                                invoke flow environment cancellationToken,
                                millisecondsTimeout = int after.TotalMilliseconds
                            )

                        return! child
                    with :? TimeoutException ->
                        return! invoke (fallback ()) environment cancellationToken
                }
                #else
                ValueTask<Exit<'value, 'error>>(
                    task {
                        let operation = invoke flow environment cancellationToken |> _.AsTask()
                        let timeoutTask = Task.Delay after
                        let! completed = Task.WhenAny([| operation :> Task; timeoutTask |])

                        if obj.ReferenceEquals(completed, timeoutTask) then
                            return! invoke (fallback ()) environment cancellationToken
                        else
                            return! operation
                    })
                #endif
            )

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
                    |> EffectFlow.fold
                        EffectFlow.ofValue
                        (fun cause ->
                            match cause with
                            | Cause.Fail error when attempt < policy.MaxAttempts && policy.ShouldRetry error ->
                                let delay = policy.Delay attempt
                                #if FABLE_COMPILER
                                async {
                                    if delay > TimeSpan.Zero then
                                        do! Async.Sleep(int delay.TotalMilliseconds)

                                    return! invoke (loop (attempt + 1)) environment cancellationToken
                                }
                                #else
                                ValueTask<Exit<'value, 'error>>(
                                    task {
                                        if delay > TimeSpan.Zero then
                                            do! Task.Delay(delay, cancellationToken)

                                        return! invoke (loop (attempt + 1)) environment cancellationToken
                                    })
                                #endif
                            | _ ->
                                EffectFlow.ofCause cause))

            loop 1

    /// <summary>Starts a flow in a new fiber without waiting for it to complete.</summary>
    /// <remarks>
    /// Forking turns a cold flow description into hot child work and returns a handle
    /// that can later be joined or interrupted. Prefer <c>zipPar</c> or <c>race</c>
    /// when the caller only needs a simple parallel composition.
    /// </remarks>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:FsFlow.Fiber`2" /> handle.</returns>
    let fork (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            #if FABLE_COMPILER
            async {
                let cts = new CancellationTokenSource()
                let operation = invoke flow environment cts.Token
                
                let! childToken = Async.StartChild(operation)
                let fiber =
                    {
                        ExitTask = childToken
                        InterruptSource = cts
                    }
                return Exit.Success fiber
            }
            #else
            let cts = new CancellationTokenSource()
            let (Flow operation) = flow
            let effect = operation environment cts.Token
            
            let fiber =
                {
                    ExitTask = effect.AsTask()
                    InterruptSource = cts
                }
            EffectFlow.ofValue fiber
            #endif
        )

    /// <summary>Waits for a fiber to complete and returns its successful value or typed failure.</summary>
    /// <remarks>
    /// Joining preserves the child workflow's error channel. If the child failed with
    /// <c>Cause.Fail</c>, the joined flow fails with the same typed error; interruption
    /// and defects remain interruption and defects.
    /// </remarks>
    /// <param name="fiber">The fiber to join.</param>
    /// <returns>A flow that completes with the fiber's outcome.</returns>
    let join (fiber: Fiber<'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ ->
            #if FABLE_COMPILER
            fiber.ExitTask
            #else
            ValueTask<Exit<'value, 'error>>(fiber.ExitTask)
            #endif
        )

    #if !FABLE_COMPILER
    /// <summary>Executes a flow and converts the final <see cref="T:FsFlow.Exit`2" /> into a standard <see cref="T:System.Result`2" />.</summary>
    /// <remarks>
    /// Interruption signals and defects are raised as exceptions in the caller's context.
    /// </remarks>
    let toResult (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Result<'value, 'error> =
        let effect = run environment flow
        effect.AsTask().GetAwaiter().GetResult()
        |> Exit.toResult
    #endif

    /// <summary>Signals a fiber to stop and waits for it to finish its cleanup.</summary>
    /// <remarks>
    /// Interruption requests cooperative cancellation through the fiber's cancellation
    /// source and then waits for the child operation to report its final
    /// <see cref="T:FsFlow.Exit`2" />.
    /// </remarks>
    /// <param name="fiber">The fiber to interrupt.</param>
    /// <returns>A flow that completes with the fiber's final outcome after interruption.</returns>
    let interrupt (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
        Flow(fun _ _ ->
            #if FABLE_COMPILER
            async {
                fiber.InterruptSource.Cancel()
                let! exit = fiber.ExitTask
                return Exit.Success exit
            }
            #else
            ValueTask<Exit<Exit<'value, 'error>, 'none>>(
                task {
                    fiber.InterruptSource.Cancel()
                    let! exit = fiber.ExitTask
                    return Exit.Success exit
                })
            #endif
        )

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
            #if FABLE_COMPILER
            async {
                let leftOp = async {
                    let! x = invoke left environment cancellationToken
                    return box x
                }
                let rightOp = async {
                    let! x = invoke right environment cancellationToken
                    return box x
                }
                let! results = Async.Parallel [| leftOp; rightOp |]
                let leftRes = unbox<Exit<'left, 'error>> results[0]
                let rightRes = unbox<Exit<'right, 'error>> results[1]
                match leftRes, rightRes with
                | Exit.Success l, Exit.Success r -> return Exit.Success (l, r)
                | Exit.Failure c, _ -> return Exit.Failure c
                | _, Exit.Failure c -> return Exit.Failure c
            }
            #else
            ValueTask<Exit<'left * 'right, 'error>>(
                task {
                    let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    
                    let (Flow leftOp) = left
                    let (Flow rightOp) = right
                    
                    let leftFiberTask = leftOp environment cts.Token |> _.AsTask()
                    let rightFiberTask = rightOp environment cts.Token |> _.AsTask()
                    
                    let! completed = Task.WhenAny(leftFiberTask, rightFiberTask)

                    if obj.ReferenceEquals(completed, leftFiberTask) then
                        match leftFiberTask.GetAwaiter().GetResult() with
                        | Exit.Failure cause ->
                            cts.Cancel()
                            return Exit.Failure cause
                        | Exit.Success leftValue ->
                            let! rightExit = rightFiberTask

                            match rightExit with
                            | Exit.Success rightValue -> return Exit.Success (leftValue, rightValue)
                            | Exit.Failure cause -> return Exit.Failure cause
                    else
                        match rightFiberTask.GetAwaiter().GetResult() with
                        | Exit.Failure cause ->
                            cts.Cancel()
                            return Exit.Failure cause
                        | Exit.Success rightValue ->
                            let! leftExit = leftFiberTask

                            match leftExit with
                            | Exit.Success leftValue -> return Exit.Success (leftValue, rightValue)
                            | Exit.Failure cause -> return Exit.Failure cause
                })
            #endif
        )

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
            #if FABLE_COMPILER
            async { return failwith "Flow.race is not supported on Fable." }
            #else
            ValueTask<Exit<'value, 'error>>(
                task {
                    let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    
                    let (Flow leftOp) = left
                    let (Flow rightOp) = right
                    
                    let leftFiberTask = leftOp environment cts.Token |> _.AsTask()
                    let rightFiberTask = rightOp environment cts.Token |> _.AsTask()
                    
                    let! completed = Task.WhenAny(leftFiberTask, rightFiberTask)
                    cts.Cancel()
                    
                    return completed.GetAwaiter().GetResult()
                })
            #endif
        )

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
            | Ok value -> EffectFlow.ofValue value
            | Error () ->
                invoke errorFlow environment cancellationToken
                |> EffectFlow.fold EffectFlow.ofError EffectFlow.ofCause)

    /// <summary>Reads the current environment as the successful flow value.</summary>
    /// <remarks>
    /// Use this when the next step genuinely needs the whole environment value, for example when
    /// passing a request context to another helper. For a single dependency or configuration value,
    /// prefer <c>Flow.read</c>; it keeps the dependency local and makes the workflow easier to scan.
    /// </remarks>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> whose successful value is the current environment.</returns>
    /// <example>
    /// <code>
    /// let myFlow = Flow.env |> Flow.map (fun env -> env)
    /// </code>
    /// </example>
    let env<'env, 'error> : Flow<'env, 'error, 'env> =
        Flow(fun environment _ -> EffectFlow.ofValue environment)

    /// <summary>Projects one value from the current environment.</summary>
    /// <remarks>
    /// This is the primary way to access app dependencies, configuration, or request metadata stored
    /// in <c>env</c>. The projection runs only when the flow is executed, so constructing the flow is
    /// still pure and side-effect free. Prefer small projections over passing a large environment
    /// deeper into reusable helpers.
    /// </remarks>
    /// <param name="projection">A function that extracts a value from the environment.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> containing the projected value.</returns>
    /// <example>
    /// <code>
    /// let myFlow = Flow.read (fun env -> env)
    /// </code>
    /// </example>
    let read (projection: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow(fun environment _ -> EffectFlow.ofValue (projection environment))

    /// <summary>Extracts a specific service from an environment that implements <c>IHas&lt;'service&gt;</c>.</summary>
    /// <remarks>This is the statically honest way to access dependencies.</remarks>
    /// <returns>A flow containing the requested service.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.service&lt;IMyService, _, _&gt;()
    /// </code>
    /// </example>
    let inline service<'service, 'env, 'error when 'env :> IHas<'service>> () : Flow<'env, 'error, 'service> =
        read (fun (env: 'env) -> env.Service)

    /// <summary>Injects a service from a dynamic IServiceProvider environment.</summary>
    /// <remarks>Trades compile-time safety for pragmatic .NET interop.</remarks>
    /// <returns>A flow containing the requested service.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.inject&lt;IMyService, _, _&gt;()
    /// </code>
    /// </example>
    let inline inject<'service, 'env, 'error when 'env :> IServiceProvider> () : Flow<'env, 'error, 'service> =
        read (fun (env: 'env) ->
            let svc = env.GetService(typeof<'service>)
            if isNull (box svc) then
                failwith $"Service {typeof<'service>.Name} was not registered in the IServiceProvider."
            else
                unbox<'service> svc
        )

    /// <summary>Transforms the successful value of a flow.</summary>
    /// <remarks>
    /// If the source <paramref name="flow" /> fails, the <paramref name="mapper" /> is not executed.
    /// The original failure cause is preserved, including typed failures, interruption, and defects.
    /// Use <c>map</c> for pure value transformations after an effect has succeeded.
    /// </remarks>
    /// <param name="mapper">A function of type <c>'value -> 'next</c> to transform the successful value.</param>
    /// <param name="flow">The source flow of type <see cref="T:FsFlow.Flow`3" /> to transform.</param>
    /// <returns>A new <see cref="T:FsFlow.Flow`3" /> with the transformed success value of type <c>'next</c>.</returns>
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
            |> EffectFlow.map mapper)

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
    /// This is the flatmap operation for <see cref="T:FsFlow.Flow`3" />. The continuation only runs
    /// when the source flow succeeds, and it receives the successful value. Use <c>bind</c> when the
    /// next effect depends on the previous result; use <c>map</c> when the next step is pure.
    /// </remarks>
    /// <param name="binder">A function that takes the successful value and returns a new flow.</param>
    /// <param name="flow">The source flow to sequence.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> representing the combined workflow.</returns>
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
            |> EffectFlow.bind (fun value -> invoke (binder value) environment cancellationToken))

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
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that preserves the original success value after the side effect.</returns>
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
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that preserves the original error after the side effect.</returns>
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
            |> EffectFlow.mapError mapper)

    /// <summary>Maps both the successful value and the failure cause of a synchronous flow.</summary>
    /// <param name="onSuccess">The function to transform the success value.</param>
    /// <param name="onFailure">The function to transform the failure cause.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A new <see cref="T:FsFlow.Flow`3" /> with transformed success and error types.</returns>
    let mapBoth
        (onSuccess: 'value -> 'next)
        (onFailure: Cause<'error> -> Cause<'nextError>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> EffectFlow.mapBoth onSuccess onFailure)

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
            |> EffectFlow.fold
                (fun value -> invoke (onSuccess value) environment cancellationToken)
                (fun cause -> invoke (onFailure cause) environment cancellationToken))

    /// <summary>Catches exceptions raised during execution and maps them to a typed error.</summary>
    /// <remarks>
    /// Exceptions that are not caught by this helper will bubble up to the caller of <see cref="run" />.
    /// This ensures that known exceptions can be handled within the flow context.
    /// </remarks>
    /// <param name="handler">A function of type <c>exn -> 'error</c> to map the exception.</param>
    /// <param name="flow">The source flow of type <see cref="T:FsFlow.Flow`3" /> to monitor.</param>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> that converts exceptions into success-path errors.</returns>
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
            #if FABLE_COMPILER
            async {
                try
                    return! (invoke flow environment cancellationToken)
                with error ->
                    return Exit.Failure (Cause.Fail (handler error))
            }
            #else
            task {
                try
                    return! (invoke flow environment cancellationToken)
                with error ->
                    return Exit.Failure (Cause.Fail (handler error))
            } |> ValueTask<Exit<'value, 'error>>
            #endif
        )

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
            |> EffectFlow.fold
                EffectFlow.ofValue
                (fun cause ->
                    match cause with
                    | Cause.Fail error -> invoke (fallback error) environment cancellationToken
                    | _ -> EffectFlow.ofCause cause))

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

    /// <summary>Runs a layer flow first, then runs a downstream flow with the layer's output as its environment.</summary>
    /// <remarks>
    /// Use this at composition boundaries where one flow builds the environment needed by another
    /// flow. Ordinary workflow code should usually consume an environment directly with
    /// <c>Flow.read</c>; <c>provideLayer</c> is for deriving or provisioning an environment before a
    /// downstream workflow starts.
    /// </remarks>
    /// <param name="layer">A flow that provides the environment required by the downstream flow.</param>
    /// <param name="flow">The flow to run with the provided environment.</param>
    /// <returns>A flow that requires only the input environment of the layer.</returns>
    /// <example>
    /// <code>
    /// let layer = Flow.succeed "test"
    /// let flow = Flow.env |> Flow.provideLayer layer
    /// </code>
    /// </example>
    let provideLayer
        (layer: Flow<'input, 'error, 'environment>)
        (flow: Flow<'environment, 'error, 'value>)
        : Flow<'input, 'error, 'value> =
        Flow(fun environment ct ->
            invoke layer environment ct
            |> EffectFlow.bind (fun innerEnvironment -> invoke flow innerEnvironment ct))

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
                    |> EffectFlow.bind (fun results ->
                        invoke (mapping value) environment ct
                        |> EffectFlow.map (fun mapped -> mapped :: results)))
                (EffectFlow.ofValue [])
            |> EffectFlow.map List.rev)

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
