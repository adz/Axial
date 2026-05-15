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
    /// <remarks>Uncaught exceptions become <c>Cause.Die</c>; cancellation becomes <c>Cause.Interrupt</c>.</remarks>
    let runFull (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Effect<'value, 'error> =
        runEffect environment cancellationToken flow

    /// <summary>Executes a flow with an explicit cancellation token.</summary>
    let runWithToken = runFull

    /// <summary>Executes a flow with the provided environment and the default cancellation token.</summary>
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

    /// <summary>Creates a successful synchronous flow.</summary>
    let ok (value: 'value) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofValue value)

    /// <summary>Alias for <c>ok</c> that reads well in some call sites.</summary>
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

    /// <summary>Alias for <c>error</c> that reads well in some call sites.</summary>
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
    /// <remarks>
    /// This is the public constructor for non-domain defects. Use <c>fail</c> for expected
    /// typed failures and <c>die</c> when the workflow should surface a bug or panic.
    /// </remarks>
    let die (exn: exn) : Flow<'env, 'error, 'value> =
        Flow(fun _ _ -> EffectFlow.ofDie exn)

    /// <summary>Lifts a <see cref="T:System.Result`2" /> into a synchronous flow.</summary>
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

    /// <summary>Runtime helpers for operational concerns like logging, timeout, retry, and cleanup.</summary>
    [<RequireQualifiedAccess>]
    module Runtime =
        /// <summary>Suspends the flow for the specified duration, observing cancellation.</summary>
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
        let now : Flow<'env, 'error, DateTimeOffset> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().Clock.UtcNow()))

        /// <summary>Writes a message through the ambient runtime logger.</summary>
        let log (message: string) : Flow<'env, 'error, unit> =
            Flow(fun _ _ ->
                RuntimeState.current().Log.Info message
                EffectFlow.ofValue ())

        /// <summary>Creates a new GUID through the ambient runtime GUID generator.</summary>
        let newGuid : Flow<'env, 'error, Guid> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().Guid.NewGuid()))

        /// <summary>Creates a random integer through the ambient runtime random generator.</summary>
        let nextInt (minInclusive: int) (maxExclusive: int) : Flow<'env, 'error, int> =
            Flow(fun _ _ ->
                EffectFlow.ofValue (RuntimeState.current().Random.NextInt minInclusive maxExclusive))

        /// <summary>Reads an environment variable from the ambient runtime environment provider.</summary>
        let tryGetEnvironmentVariable (name: string) : Flow<'env, 'error, string option> =
            Flow(fun _ _ -> EffectFlow.ofValue (RuntimeState.current().EnvironmentVariables.TryGet name))

    /// <summary>Starts a flow in a new fiber without waiting for it to complete.</summary>
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

    /// <summary>Waits for a fiber to complete and returns its final outcome.</summary>
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

    /// <summary>Reads the current environment as the successful flow value.</summary>
    /// <remarks>
    /// Use this when the next step genuinely needs the whole environment value, for example when
    /// passing a request context to another helper. For a single dependency or configuration value,
    /// prefer <c>Flow.read</c>; it keeps the dependency local and makes the workflow easier to scan.
    /// </remarks>
    /// <returns>A <see cref="T:FsFlow.Flow`3" /> whose successful value is the current environment.</returns>
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
    let read (projection: 'env -> 'value) : Flow<'env, 'error, 'value> =
        Flow(fun environment _ -> EffectFlow.ofValue (projection environment))

    /// <summary>Transforms the successful value of a flow.</summary>
    /// <remarks>
    /// If the source <paramref name="flow" /> fails, the <paramref name="mapper" /> is not executed.
    /// The original failure cause is preserved, including typed failures, interruption, and defects.
    /// Use <c>map</c> for pure value transformations after an effect has succeeded.
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

    /// <summary>Sequences a dependent flow after a successful value.</summary>
    /// <remarks>
    /// This is the flatmap operation for <see cref="T:FsFlow.Flow`3" />. The continuation only runs
    /// when the source flow succeeds, and it receives the successful value. Use <c>bind</c> when the
    /// next effect depends on the previous result; use <c>map</c> when the next step is pure.
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

    /// <summary>Runs an effect on success and preserves the original value.</summary>
    /// <remarks>
    /// Use this for logging, telemetry, metrics, or audit steps that should observe a successful
    /// value without replacing it. If the <paramref name="binder" /> flow fails, that failure becomes
    /// the result of the whole flow, because the tap effect is still part of the workflow.
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

    /// <summary>Runs two flows sequentially and combines their successful values into a tuple.</summary>
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

    /// <summary>Runs a flow against an environment derived from the outer environment.</summary>
    /// <remarks>
    /// Use this to embed a smaller workflow inside a larger application environment without changing
    /// the smaller workflow's type. The mapping is applied at execution time. This is useful for
    /// preserving narrow helper signatures while still running everything from one app boundary.
    /// </remarks>
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
