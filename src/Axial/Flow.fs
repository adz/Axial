namespace Axial

open System
open System.Threading
open System.Threading.Tasks

#if !FABLE_COMPILER
type private BindAwaiter<'env, 'error, 'value, 'next>
    (source: Task<Exit<'value, 'error>>, binder: 'value -> Flow<'env, 'error, 'next>) =
    interface IFlowAwaiter<'env, 'error, 'next> with
        member _.Pending = source :> Task

        member _.Resume() =
            // The runner has already awaited this Task. AsTask consumed the source ValueTask exactly once;
            // reading the cached Task result here also supports executions backed by IValueTaskSource.
            match source.Result with  // axial-allow-AXG001: task already awaited
            | Exit.Success value -> FlowStep.Next(binder value)
            | Exit.Failure cause -> FlowStep.Failed cause
#endif

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Resource =
    /// <summary>Describes acquisition together with a release flow registered in the current Flow scope.</summary>
    /// <remarks>
    /// The release runs when the scope closes, without interruption and with the environment captured at
    /// acquisition. It cannot fail with a typed error; a defect it raises becomes a defect of the flow that closes the
    /// scope, after the scope's other releases have run.
    /// </remarks>
    /// <example>
    /// <code>
    /// Resource.create openConnection (fun connection -&gt; Flow.delay (fun () -&gt; connection.Close(); Flow.succeed ()))
    /// </code>
    /// </example>
    let create
        (acquire: Flow<'env, 'error, 'value>)
        (release: 'value -> Flow<'env, Never, unit>)
        : Resource<'env, 'error, 'value> =
        Resource(acquire, release)

    /// <summary>Describes a release flow, registered in the current Flow scope, as a unit-valued resource.</summary>
    let finalizer (release: Flow<'env, Never, unit>) : Resource<'env, 'error, unit> =
        Resource(Flow(fun _ _ -> Execution.ofValue ()), fun () -> release)

/// <summary>Holds at most one running fiber; forking into it with <c>Flow.forkReplacing</c> interrupts the previous one.</summary>
/// <remarks>Create one with <c>FiberSlot.make</c>. Use it where only the newest request matters, such as a search box.</remarks>
[<Sealed>]
type FiberSlot<'error, 'value> internal () =
    let gate = obj ()
    let mutable current: Fiber<'error, 'value> option = None

    member internal _.Replace(fiber: Fiber<'error, 'value>) =
        Platform.lock gate (fun () ->
            let previous = current
            current <- Some fiber
            previous)

    member internal _.Take() =
        Platform.lock gate (fun () ->
            let previous = current
            current <- None
            previous)

/// <summary>Holds at most one running fiber per key; forking into a key with <c>Flow.forkReplacingKey</c> interrupts that key's previous fiber.</summary>
/// <remarks>Create one with <c>FiberSlot.makeKeyed</c>. A key's entry is removed when its fiber settles.</remarks>
[<Sealed>]
type KeyedFiberSlot<'key, 'error, 'value when 'key: equality> internal () =
    let gate = obj ()
    let entries = System.Collections.Generic.Dictionary<'key, obj * Fiber<'error, 'value>>(HashIdentity.Structural)

    member internal _.Replace(key: 'key, marker: obj, fiber: Fiber<'error, 'value>) =
        Platform.lock gate (fun () ->
            let previous =
                match entries.TryGetValue key with
                | true, (_, previous) -> Some previous
                | _ -> None

            entries[key] <- (marker, fiber)
            previous)

    member internal _.Remove(key: 'key, marker: obj) =
        Platform.lock gate (fun () ->
            match entries.TryGetValue key with
            | true, (owner, _) when obj.ReferenceEquals(owner, marker) -> entries.Remove key |> ignore
            | _ -> ())

    member internal _.TakeAll() =
        Platform.lock gate (fun () ->
            let fibers = [ for entry in entries.Values -> snd entry ]
            entries.Clear()
            fibers)

    member internal _.Count = Platform.lock gate (fun () -> entries.Count)

module Flow =
    let rec private cleanupCause (error: exn) : Cause<'error> =
        match error with
        | :? ReleaseCauseException as release -> Cause.map (fun _ -> Unchecked.defaultof<'error>) release.Cause
#if !FABLE_COMPILER
        | :? AggregateException as aggregate when aggregate.InnerExceptions.Count > 0 ->
            aggregate.InnerExceptions |> Seq.map cleanupCause |> Seq.reduce Cause.thenCause
#endif
        | _ -> Execution.causeOfException error

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
            | Some error, _ -> Exit.Failure (cleanupCause error)
            | None, Some result -> result
            | None, None -> Exit.Failure (Cause.Die (InvalidOperationException missingOutcomeMessage))

        match cleanupError, primary with
        | Some error, Exit.Failure cause ->
            Exit.Failure (Cause.thenCause cause (cleanupCause error))
        | Some error, Exit.Success _ ->
            Exit.Failure (cleanupCause error)
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

    /// Reports every defect inside a runtime-discarded exit (for example, a timeout loser) as unobserved.
    /// Such exits are dropped without a fiber handle, so nobody can ever observe them.
    let private reportDiscardedExit (observer: FiberObserver) (exit: Exit<'value, 'error>) : unit =
        match exit with
        | Exit.Success _ -> ()
        | Exit.Failure cause ->
            for defect in Cause.defects cause do
                FiberObserver.notifyUnobservedDefect observer None defect

    let internal reportUnobservedCause (cause: Cause<'error>) : unit =
        let observer = RuntimeState.current().Observer
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

    // Closes scopes in order and waits for their cleanup, folding a cleanup failure into the exit as a defect.
    let private closeScopes
        (cancellationToken: CancellationToken)
        (scopes: Scope list)
        (exit: Exit<'value, 'error>)
        : Execution<'value, 'error> =
        Platform.execution {
            let result = ref exit

            for scope in scopes do
                try
                    do! scope.Close cancellationToken
                with error ->
                    result.Value <-
                        combineCleanup (Some error) None (Some result.Value) "Branch cleanup produced no outcome."

            return result.Value
        }
        |> Platform.ofAwaitable

    // Race closes one scope at a time. Keep an inline close inline rather than allocating the list traversal
    // and completed task used by closeScopes; a suspended or faulted close still observes the same cleanup cause.
    let private closeRaceScope
        (cancellationToken: CancellationToken)
        (scope: Scope)
        (exit: Exit<'value, 'error>)
        : Execution<'value, 'error> =
#if FABLE_COMPILER
        closeScopes cancellationToken [ scope ] exit
#else
        try
            let closing = scope.Close cancellationToken
            if closing.IsCompletedSuccessfully then
                Platform.ofExit exit
            else
                task {
                    try
                        do! closing
                        return exit
                    with error ->
                        return combineCleanup (Some error) None (Some exit) "Branch cleanup produced no outcome."
                }
                |> Platform.ofAwaitable
        with error ->
            combineCleanup (Some error) None (Some exit) "Branch cleanup produced no outcome."
            |> Platform.ofExit
#endif

    // Registers a finalizer with `scope` and succeeds with `value`. A scope that has closed can no longer run the
    // finalizer, and whoever owned it has finished (a race loser that acquires after the race returned), so the
    // finalizer runs now and the flow is interrupted.
    let private registerOrRelease
        (scope: Scope)
        (finalizer: Platform.Finalizer)
        (value: 'value)
        : Execution<'value, 'error> =
        match scope.TryRegister finalizer with
        | ValueSome _ -> Execution.ofValue value
        | ValueNone ->
            let releasing = new Scope()
            releasing.AddFinalizer finalizer
            Platform.Cancellation.uninterruptible (fun token ->
                closeScopes token [ releasing ] (Exit.Failure Cause.Interrupt))

    // A release that ended in failure, as the exception the scope's finalizer raises. The scope folds it back into
    // the closing flow's cause as a defect.
    let private releaseFailure (cause: Cause<Never>) : exn =
        ReleaseCauseException(Cause.map Never.absurd cause)

#if FABLE_COMPILER
    // On JavaScript the runtime cell is the ambient slot, so the release installs its runtime directly; the
    // finalizer gives the closing flow its own value back once the release completes. Going through
    // `RuntimeState.withRuntime` here (an async try/finally on a fresh ambient trampoline, nested inside the closing
    // scope's finalizer loop) stalls the torture suite's stream scenario.
    let private runRelease (runtime: RuntimeContext) (environment: 'env) (release: unit -> Flow<'env, Never, unit>) =
        try
            Platform.restoreAmbient (box runtime)
            Platform.Cancellation.uninterruptible (invoke (release ()) environment)
        with error ->
            Platform.ofExit (Exit.Failure(Cause.Die error))

#endif
#if !FABLE_COMPILER
    [<Sealed>]
    type private ReleaseFinalizer<'env, 'value>
        (acquiredIn: RuntimeContext, environment: 'env, release: 'value -> Flow<'env, Never, unit>, value: 'value) =
        inherit FSharpFunc<CancellationToken, Platform.Deed>()

        override _.Invoke _ =
            let scope = new Scope()
            let runtime =
                { acquiredIn with
                    Scope = scope
                    BranchToken = Platform.Cancellation.never
                    OuterToken = Platform.Cancellation.never }
            let previous = RuntimeState.current ()
            let execution =
                try
                    RuntimeState.enter runtime
                    try Platform.Cancellation.uninterruptible (invoke (release value) environment)
                    with error -> Platform.ofExit (Exit.Failure(Cause.Die error))
                finally RuntimeState.enter previous
            let finish (exit: Exit<unit, Never>) (closing: Platform.Deed) : Platform.Deed =
                task {
                    let mutable closeError = None
                    try do! closing
                    with error -> closeError <- Some error
                    match combineCleanup closeError None (Some exit) "A resource release produced no outcome." with
                    | Exit.Success() -> ()
                    | Exit.Failure cause -> return raise (releaseFailure cause)
                } :> Task
            if execution.IsCompletedSuccessfully then
                let exit = execution.Result
                let closing = Platform.Cancellation.uninterruptible scope.Close
                if closing.IsCompletedSuccessfully then
                    match exit with
                    | Exit.Success() -> Task.CompletedTask
                    | Exit.Failure cause -> Task.FromException(releaseFailure cause)
                else finish exit closing
            else
                task {
                    let! exit = execution
                    return! finish exit (Platform.Cancellation.uninterruptible scope.Close)
                } :> Task
#endif

    // Turns a release flow into a scope finalizer. The release runs with the environment and runtime captured now (at
    // acquisition), in a scope of its own, and without the closing flow's cancellation token, so it cannot be
    // interrupted. A failed release, or a failure closing its scope, makes the finalizer raise.
    let internal releaseFinalizer (environment: 'env) (release: 'value -> Flow<'env, Never, unit>) (value: 'value) : Platform.Finalizer =
        let acquiredIn = RuntimeState.current ()

#if !FABLE_COMPILER
        unbox<Platform.Finalizer> (box (ReleaseFinalizer(acquiredIn, environment, release, value)))
#else
        fun _ ->
            let scope = new Scope()

            let runtime =
                { acquiredIn with
                    Scope = scope
                    BranchToken = Platform.Cancellation.never
                    OuterToken = Platform.Cancellation.never }

            let finish (exit: Exit<unit, Never>) (closing: Platform.Deed) : Platform.Deed =
                Platform.execution {
                    let mutable closeError = None

                    try
                        do! closing
                    with error ->
                        closeError <- Some error

                    match combineCleanup closeError None (Some exit) "A resource release produced no outcome." with
                    | Exit.Success() -> ()
                    | Exit.Failure cause -> return raise (releaseFailure cause)
                }

            async {
                // The release resumes from its own runtime; give the closing flow its ambient runtime back, or it
                // carries on in the release's closed scope with inert tokens and later timers and forks are lost.
                let captured = Platform.captureAmbient ()
                let! exit = runRelease runtime environment (fun () -> release value)
                Platform.restoreAmbient captured
                return! finish exit (Platform.Cancellation.uninterruptible scope.Close)
            }
#endif

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
    /// <summary>Registers a disposable resource with the current runtime scope.</summary>
    /// <param name="resource">The disposable resource to close when the current scope closes.</param>
    /// <returns>A flow that registers the resource.</returns>
    let scopeDisposable
        (resource: IDisposable)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            if isNull (box resource) then
                nullArg (nameof resource)

            registerOrRelease
                (RuntimeState.current().Scope)
                (fun _ ->
                    resource.Dispose()
                    Platform.completedDeed ())
                ())

    /// <summary>Registers an asynchronously disposable resource with the current runtime scope.</summary>
    /// <param name="resource">The async disposable resource to close when the current scope closes.</param>
    /// <returns>A flow that registers the resource.</returns>
    let scopeAsyncDisposable
        (resource: IAsyncDisposable)
        : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            if isNull (box resource) then
                nullArg (nameof resource)

            registerOrRelease (RuntimeState.current().Scope) (fun _ -> Platform.disposeAsyncDeed resource) ())

#endif

    /// <summary>Acquires a described resource and registers its release with the current runtime scope.</summary>
    /// <remarks>
    /// The resource is a scoped resource: it stays open until the current scope closes. Inside a forked fiber that is
    /// the fiber's own scope, which closes when the fiber settles; inside a branch of <c>zipPar</c>, <c>race</c>,
    /// <c>sequencePar</c>, or <c>traversePar</c>, the resource moves to the caller's scope if the caller receives the
    /// branch's value. Use <c>forkResource</c> to acquire it in the background and keep it. A flow that finishes
    /// acquiring after its scope has closed, such as a <c>race</c> loser, releases the resource at once and is
    /// interrupted.
    /// </remarks>
    /// <param name="resource">The acquisition and release description.</param>
    /// <returns>A flow that succeeds with the acquired value.</returns>
    let scopeResource (Resource(acquire, release)) : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke acquire environment cancellationToken
            |> Execution.bind (fun value ->
                registerOrRelease (RuntimeState.current().Scope) (releaseFinalizer environment release value) value))

    /// <summary>Acquires a value and registers its release with the current runtime scope.</summary>
    /// <remarks>Same as <c>Resource.create acquire release |&gt; Flow.scopeResource</c>; see <c>Resource.create</c> for how the release runs.</remarks>
    /// <param name="acquire">The flow that acquires the value.</param>
    /// <param name="release">The release flow run when the current scope closes.</param>
    /// <returns>A flow that succeeds with the acquired value.</returns>
    let scopeAcquireRelease
        (acquire: Flow<'env, 'error, 'resource>)
        (release: 'resource -> Flow<'env, Never, unit>)
        : Flow<'env, 'error, 'resource> =
        Resource.create acquire release |> scopeResource

    /// <summary>Registers a release flow with the current runtime scope.</summary>
    /// <remarks>
    /// Use this when cleanup should run when the surrounding runtime, fiber, or layer scope closes, rather than when
    /// the current expression ends (<c>Flow.ensuring</c>). The release runs without interruption and with the current
    /// environment; a defect it raises becomes a defect of the flow that closes the scope.
    /// </remarks>
    /// <param name="release">The release flow run when the current scope closes.</param>
    /// <returns>A flow that registers the release.</returns>
    let scopeFinalizer (release: Flow<'env, Never, unit>) : Flow<'env, 'error, unit> =
        Flow(fun environment _ -> registerOrRelease (RuntimeState.current().Scope) (releaseFinalizer environment (fun () -> release) ()) ())

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

    // Parallel combinators run their flows in a branch scope, a child of the caller's scope, then decide whether the
    // caller receives the branch's value: zipPar and sequencePar share one branch among all their flows, race gives
    // each flow its own. A branch whose value the caller receives keeps its scope: it stays registered with the
    // caller's scope and closes with it, so its scoped resources live as if the caller had acquired them. Any
    // other branch (a race loser, a sibling interrupted by a failure) has its scope closed at once. The scope attaches
    // to the caller's only when the branch first registers something (Scope.Branch), so a branch that acquires nothing
    // costs no registration. The branch runs under a token its combinator cancels to stop it; a fiber forked in the
    // branch follows the caller's token (`outerToken`) instead, as the branch's scoped resources follow its scope.
    let private branchIn
        (parent: RuntimeContext)
        (outerToken: CancellationToken)
        (flow: Flow<'env, 'error, 'value>)
        (environment: 'env)
        : struct (Scope * (CancellationToken -> Execution<'value, 'error>)) =
        let branchScope = Scope.Branch parent.Scope

        let run token =
            let branchRuntime = parent |> RuntimeContext.inBranch branchScope outerToken token
            RuntimeState.withRuntime branchRuntime (fun () -> invoke flow environment token)

        struct (branchScope, run)

    // The caller receives this branch's value, so its scope stays with the caller's scope. An empty scope is closed
    // so that a long-lived caller does not retain one child per parallel call.
    let private keepBranch (branchScope: Scope) : unit = branchScope.CloseIfEmpty()

    // Runs zipPar's or sequencePar's flows as one branch of the caller, under one cancellation source that every
    // zipParExecution in the branch shares, so the first failure anywhere in it interrupts the rest. The branch's
    // scoped resources move to the caller's scope when it succeeds and are released before it returns when it fails.
    // The source is detached from the caller's token once the branch settles. `start` runs inside the branch and
    // starts its flows under the source; it is a plain function rather than a Flow so that each call builds nothing
    // extra.
    let private parallelBranch
        (start: CancellationTokenSource -> 'env -> Execution<'value, 'error>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let source = new CancellationTokenSource()
            let unlink = Platform.linkTo cancellationToken source
            let parent = RuntimeState.current()
            let branchScope = Scope.Branch parent.Scope
            let branchRuntime = parent |> RuntimeContext.inBranch branchScope cancellationToken source.Token

            let running =
                try
                    RuntimeState.withRuntime branchRuntime (fun () -> start source environment)
                with _ ->
                    unlink ()
                    reraise ()

            let settled = running |> Platform.afterSettled unlink

#if FABLE_COMPILER
            settled
            |> Execution.fold
                (fun value ->
                    keepBranch branchScope
                    Execution.ofValue value)
                (fun cause -> closeScopes cancellationToken [ branchScope ] (Exit.Failure cause)))
#else
            if settled.IsCompletedSuccessfully then
                match settled.Result with
                | Exit.Success value ->
                    keepBranch branchScope
                    Execution.ofValue value
                | Exit.Failure cause -> closeScopes cancellationToken [ branchScope ] (Exit.Failure cause)
            else
                settled
                |> Execution.fold
                    (fun value ->
                        keepBranch branchScope
                        Execution.ofValue value)
                    (fun cause -> closeScopes cancellationToken [ branchScope ] (Exit.Failure cause)))
#endif

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
                ValueTask<Exit<'value, 'error>>(Task.Run(run, Platform.Cancellation.never))
#endif
        )

    /// <summary>Starts a flow at the requested execution location.</summary>
    /// <remarks>
    /// <para>
    /// This changes only where the wrapped flow starts. It keeps the same environment, cancellation token, runtime,
    /// and scope. Cancellation remains cooperative: the wrapped flow observes interruption at its normal check
    /// points, such as <c>Flow.ensureNotCanceled</c>. If the token is cancelled before scheduling or while waiting in
    /// the scheduler's queue, the flow returns interruption without starting the wrapped flow.
    /// </para>
    /// <para>
    /// On .NET, <c>ThreadPool</c> uses <see cref="P:System.Threading.Tasks.TaskScheduler.Default" />.
    /// <c>DedicatedThread</c> gives the synchronous part before the first incomplete await a dedicated thread; later
    /// continuations normally resume on the thread pool. <c>Scheduler</c> captures the supplied scheduler across
    /// awaits inside the wrapped flow; the caller's continuation resumes on its original scheduler. On JavaScript
    /// every location runs inline because JavaScript has one thread.
    /// </para>
    /// </remarks>
    /// <param name="location">Where the wrapped flow starts.</param>
    /// <param name="source">The flow to start there.</param>
    /// <returns>The wrapped flow, or <paramref name="source" /> unchanged for <c>Inline</c>.</returns>
    /// <example>
    /// <code>
    /// let parallel = jobs |&gt; List.map (fun job -&gt; compute job |&gt; Flow.runOn RunOn.ThreadPool) |&gt; Flow.sequencePar
    /// </code>
    /// </example>
    /// <platforms>Fable compatible; JavaScript runs every case inline. Custom schedulers are .NET only.</platforms>
    let runOn (location: RunOn) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
#if FABLE_COMPILER
        source
#else
        let schedule (options: TaskCreationOptions) (scheduler: TaskScheduler) =
            Flow(fun environment cancellationToken ->
                if cancellationToken.IsCancellationRequested then
                    Execution.ofCause Cause.Interrupt
                else
                    let runScheduled () =
                        if cancellationToken.IsCancellationRequested then
                            Task.FromResult(Exit.Failure Cause.Interrupt)
                        else
                            (invoke source environment cancellationToken).AsTask()

                    Task.Factory.StartNew<Task<Exit<'value, 'error>>>(
                        runScheduled,
                        Platform.Cancellation.never,
                        options,
                        scheduler)
                    |> fun outer -> outer.Unwrap()
                    |> ValueTask<Exit<'value, 'error>>)

        match location with
        | RunOn.Inline -> source
        | RunOn.ThreadPool -> schedule TaskCreationOptions.DenyChildAttach TaskScheduler.Default
        | RunOn.DedicatedThread ->
            schedule
                (TaskCreationOptions.DenyChildAttach ||| TaskCreationOptions.LongRunning)
                TaskScheduler.Default
        | RunOn.Scheduler scheduler -> schedule TaskCreationOptions.DenyChildAttach scheduler
        | RunOn.Adaptive state ->
            let threadPool = schedule TaskCreationOptions.DenyChildAttach TaskScheduler.Default
            Flow(fun environment cancellationToken ->
                match state.Decision with
                | 1 -> invoke source environment cancellationToken
                | 2 -> invoke threadPool environment cancellationToken
                | _ ->
                    let started = Platform.monotonicNow ()
                    try
                        invoke source environment cancellationToken
                    finally
                        state.Observe(Platform.monotonicNow () - started))
#endif

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

    /// <summary>Creates a flow from an existing cold task value.</summary>
    /// <remarks>The operation runs on each execution. Thrown exceptions are recorded as defects.</remarks>
    /// <param name="operation">The cold task to run on each execution.</param>
    /// <platforms>.NET only</platforms>
    let fromColdTask (operation: ColdTask<'value>) : Flow<'env, 'error, 'value> =
        let (ColdTask factory) = operation
        TaskInterop.from Exit.Success factory

    /// <summary>Creates a flow from an existing cold task value whose <c>Error</c> enters the typed error channel.</summary>
    /// <remarks>The operation runs on each execution. Thrown exceptions are recorded as defects.</remarks>
    /// <param name="operation">The cold task to run on each execution.</param>
    /// <platforms>.NET only</platforms>
    let fromColdTaskResult (operation: ColdTask<Result<'value, 'error>>) : Flow<'env, 'error, 'value> =
        let (ColdTask factory) = operation
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

    /// <summary>Same as <c>ok</c>.</summary>
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

    /// <summary>Same as <c>error</c>.</summary>
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

    /// Overrides the clock within this flow and its descendants.
    let withClock (clock: IClock) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (fun runtime -> { runtime with Platform = { runtime.Platform with Clock = clock } }) source
    /// Overrides random values within this flow and its descendants.
    let withRandom (random: IRandom) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (fun runtime -> { runtime with Platform = { runtime.Platform with Random = random } }) source
    /// Overrides GUID generation within this flow and its descendants.
    let withGuid (guid: IGuid) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (fun runtime -> { runtime with Platform = { runtime.Platform with Guid = guid } }) source
    /// Replaces the log destination within this flow and its descendants.
    let withLogSink (sink: ILogSink) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (fun runtime -> { runtime with LogSink = sink }) source
    /// Decorates the destination already installed in this scope.
    let mapLogSink (mapper: ILogSink -> ILogSink) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (fun runtime -> { runtime with LogSink = mapper runtime.LogSink }) source
    /// Replaces both telemetry slots within this flow and its descendants.
    let withTelemetry (telemetry: ITelemetry) (source: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        withRuntime (fun runtime ->
            { runtime with
                AnnotationSink = fun name value ->
                    // axial-note AXG010: telemetry is an observation and cannot change the workflow outcome.
                    try telemetry.Annotate(name, value) with _ -> ()
                TelemetrySink = fun attribute ->
                    // axial-note AXG010: telemetry is an observation and cannot change the workflow outcome.
                    try telemetry.Attribute attribute with _ -> () }) source

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
    /// and call <c>registry.DumpAt(clock)</c> (or <c>registry.Snapshot()</c>) whenever a live fiber tree is needed.
    /// </remarks>
    /// <param name="registry">The registry that receives fiber lifecycle events.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers are tracked in the registry.</returns>
    let withFiberRegistry
        (registry: FiberRegistry)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        withRuntime
            (fun runtime ->
                runtime
                |> RuntimeContext.withObserver (FiberObserver.compose runtime.Observer registry.Observer)
                |> RuntimeContext.withRegistry registry)
            flow

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
            // axial-note AXG010: annotation sink failures must not replace the annotated workflow's result.
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
    /// let sumSquares (values: int list) : Flow&lt;unit, string, int&gt; =
    ///     flow {
    ///         let mutable total = 0
    ///         for value in values do
    ///             do! Flow.ensureNotCanceled
    ///             total &lt;- total + value * value
    ///         return total
    ///     }
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
                            match Cause.untraced cause with
                            | Cause.Die error -> convert error (fun () -> Execution.ofCause cause)
                            | _ -> Execution.ofCause cause))
                (fun error -> convert error (fun () -> raise error)))

    /// <summary>Suspends the flow for the specified duration, observing cancellation.</summary>
    /// <param name="delay">The duration to sleep.</param>
    /// <returns>A flow that completes after the specified delay, or is interrupted if cancelled first.</returns>
    let sleep<'env, 'error> (delay: TimeSpan) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken -> Platform.timeOfClock (RuntimePlatform.current().Platform.Clock) |> fun time -> time.Sleep(delay, cancellationToken))

    /// <summary>Reads the current runtime scope.</summary>
    /// <returns>A flow that succeeds with the scope owned by the current execution boundary.</returns>
    let scope<'env, 'error> : Flow<'env, 'error, Scope> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Scope))

    /// <summary>Reads the current runtime annotations.</summary>
    /// <returns>A flow that succeeds with the ambient annotation map.</returns>
    let annotations<'env, 'error> : Flow<'env, 'error, Map<string, string>> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Context.Annotations))

    /// <summary>Reads the current trace id annotation, if one is present.</summary>
    /// <remarks>Set it with <c>Flow.withTraceId</c>.</remarks>
    /// <returns>A flow that succeeds with the ambient <c>trace_id</c> value, if present.</returns>
    let traceId<'env, 'error> : Flow<'env, 'error, string option> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Context.Annotations |> Map.tryFind "trace_id"))

    /// <summary>Reads the current fiber id from the ambient runtime context.</summary>
    /// <remarks>
    /// The root workflow runs on a fiber id of its own; every <c>Flow.fork</c> child gets a fresh id.
    /// Telemetry integrations use this to correlate workflow spans with fiber lifecycle events.
    /// </remarks>
    /// <returns>A flow that succeeds with the current <see cref="T:Axial.FiberId" />.</returns>
    let fiberId<'env, 'error> : Flow<'env, 'error, FiberId> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().FiberId))

    // A timed flow runs as a branch: the caller keeps its scoped resources only when it finishes in time and
    // succeeds. When the timeout wins, the interrupted flow's scope is closed before the fallback runs.
    let private timeoutBranch<'env, 'error, 'value>
        (environment: 'env)
        (cancellationToken: CancellationToken)
        (after: TimeSpan)
        (flow: Flow<'env, 'error, 'value>)
        (onTimeout: unit -> Execution<'value, 'error>)
        : Execution<'value, 'error> =
        let parent = RuntimeState.current()
        let struct (branchScope, operation) = branchIn parent cancellationToken flow environment
        let timedOut = ref false

        Platform.timeoutExecution
            (Platform.timeOfClock (RuntimePlatform.current().Platform.Clock))
            after
            operation
            cancellationToken
            (reportDiscardedExit parent.Observer)
            (fun () ->
                timedOut.Value <- true

                closeScopes cancellationToken [ branchScope ] (Exit.Success())
                |> Execution.bind onTimeout)
        |> Execution.fold
            (fun value ->
                if not timedOut.Value then
                    keepBranch branchScope

                Execution.ofValue value)
            (fun cause ->
                if timedOut.Value then
                    Execution.ofCause cause
                else
                    closeScopes cancellationToken [ branchScope ] (Exit.Failure cause))

    /// <summary>Fails with the supplied typed error when the flow does not complete before the timeout.</summary>
    /// <remarks>The timed-out flow is interrupted. The timeout owns that interruption, so it may report it as a typed error. The flow runs in its own scope: its scoped resources move to the caller's scope when it succeeds in time, and are released when it fails or times out. <c>timeoutToOk</c> and <c>timeoutWith</c> treat scoped resources the same way.</remarks>
    /// <param name="after">The timeout duration.</param>
    /// <param name="timeoutError">The typed error returned when the timeout wins.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that returns the source outcome or the timeout error.</returns>
    let timeout<'env, 'error, 'value>
        (after: TimeSpan)
        (timeoutError: 'error)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            timeoutBranch environment cancellationToken after flow
                (fun () -> Platform.ofExit (Exit.Failure(Cause.Fail timeoutError))))

    /// <summary>Returns the supplied success value when the flow does not complete before the timeout.</summary>
    /// <param name="after">The timeout duration.</param>
    /// <param name="value">The success value returned when the timeout wins.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that returns the source outcome or the supplied success value.</returns>
    let timeoutToOk<'env, 'error, 'value>
        (after: TimeSpan)
        (value: 'value)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            timeoutBranch environment cancellationToken after flow
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
    let timeoutWith<'env, 'error, 'value>
        (after: TimeSpan)
        (fallback: unit -> Flow<'env, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            timeoutBranch environment cancellationToken after flow
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
        (time: Platform.ITimeSource)
        (attemptScope: Scope)
        (onCleanupError: exn -> Cause<'error>)
        (delay: TimeSpan)
        (cancellationToken: CancellationToken)
        : Execution<unit, 'error> =
        let deadline = time.Now() + delay

        closeSuperseded attemptScope onCleanupError cancellationToken
        |> Execution.bind (fun () ->
            let remaining = deadline - time.Now()
            time.Sleep((if remaining > TimeSpan.Zero then remaining else TimeSpan.Zero), cancellationToken))

    let private scheduleContext (time: Platform.ITimeSource) runState attempt loopStarted executionStarted : ScheduleContext =
        { Attempt = attempt
          LoopStarted = loopStarted
          ExecutionStarted = executionStarted
          ExecutionEnded = time.Now()
          RunState = runState }

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
    let retry<'env, 'error, 'value, 'output>
        (schedule: Schedule<'env, 'error, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        // A loop rather than recursion, so retrying for the life of an application runs in constant memory.
        Flow(fun environment cancellationToken ->
            let time = Platform.timeOfClock (RuntimePlatform.current().Platform.Clock)
            let runState = ScheduleContext.newRunState ()
            let loopStarted = time.Now()

            Execution.loop (0, loopStarted) (fun (attempt, executionStarted) ->
                let attemptScope, execution = runAttempt flow environment cancellationToken

                execution
                |> Execution.fold
                    (Platform.Break >> Execution.ofValue)
                    (fun cause ->
                        match Cause.untraced cause with
                        | Cause.Fail error ->
                            let context = scheduleContext time runState attempt loopStarted executionStarted

                            Schedule.decide schedule error context environment cancellationToken
                            |> Execution.bind (fun (decision, delay) ->
                                match decision with
                                | Some _ ->
                                    supersedeThenWait
                                        time
                                        attemptScope
                                        (fun cleanupError -> Cause.thenCause cause (Execution.causeOfException cleanupError))
                                        delay
                                        cancellationToken
                                    |> Execution.map (fun () -> Platform.Continue(attempt + 1, time.Now()))
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
    let repeat<'env, 'error, 'value, 'output>
        (schedule: Schedule<'env, 'value, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        // A loop rather than recursion, so a schedule that repeats for the life of an application (a control
        // scan, a heartbeat) runs in constant memory.
        Flow(fun environment cancellationToken ->
            let time = Platform.timeOfClock (RuntimePlatform.current().Platform.Clock)
            let runState = ScheduleContext.newRunState ()
            let loopStarted = time.Now()
            let firstScope, first = runAttempt flow environment cancellationToken

            first
            |> Execution.bind (fun firstValue ->
                Execution.loop (0, firstValue, firstScope, loopStarted) (fun (attempt, lastValue, lastScope, executionStarted) ->
                    let context = scheduleContext time runState attempt loopStarted executionStarted

                    Schedule.decide schedule lastValue context environment cancellationToken
                    |> Execution.bind (fun (decision, (delay: TimeSpan)) ->
                        match decision with
                        | Some _ ->
                            supersedeThenWait time lastScope Execution.causeOfException delay cancellationToken
                            |> Execution.bind (fun () ->
                                let started = time.Now()
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
    let supervise<'env, 'error, 'value, 'output>
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
            let time = Platform.timeOfClock (RuntimePlatform.current().Platform.Clock)
            let runState = ScheduleContext.newRunState ()
            let loopStarted = time.Now()

            Execution.loop (0, loopStarted) (fun (attempt, executionStarted) ->
                let attemptScope, execution = runAttempt flow environment cancellationToken

                execution
                |> Execution.fold
                    (Platform.Break >> Execution.ofValue)
                    (fun cause ->
                        match restartable cause with
                        | Some defect ->
                            let context = scheduleContext time runState attempt loopStarted executionStarted

                            Schedule.decide schedule defect context environment cancellationToken
                            |> Execution.bind (fun (decision, delay) ->
                                match decision with
                                | Some _ ->
                                    supersedeThenWait
                                        time
                                        attemptScope
                                        (fun cleanupError -> Cause.thenCause cause (Execution.causeOfException cleanupError))
                                        delay
                                        cancellationToken
                                    |> Execution.map (fun () -> Platform.Continue(attempt + 1, time.Now()))
                                | None -> Execution.ofCause cause)
                        | None -> Execution.ofCause cause)))

    /// A graceful fiber's stop request and how long to wait after it before interrupting.
    type private GracefulStop<'env> = { Stop: Flow<'env, Never, unit>; Grace: TimeSpan }

    /// Which scope supervises a fiber: closing it interrupts the fiber and waits for its cleanup.
    [<RequireQualifiedAccess>]
    type private ForkOwner =
        /// The forking scope (Flow.fork).
        | Caller
        /// The root run's scope (Flow.disconnect): the caller does not wait, the application does.
        | Root
        /// No scope; an internal caller manages the fiber through its handle.
        | Unowned

    let private forkWith<'env, 'error, 'value, 'none>
        (owner: ForkOwner)
        (name: string option)
        (graceful: GracefulStop<'env> option)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            let parentRuntime = RuntimeState.current()
            let observer = parentRuntime.Observer
            let clock = RuntimePlatform.current().Platform.Clock
            let time = Platform.timeOfClock clock

            let metadata: FiberMetadata =
                {
                    Id = FiberId.next ()
                    Name = name
                    ParentId = Some parentRuntime.FiberId
                    Annotations = parentRuntime.Context.Annotations
                    // Fiber lifecycle bookkeeping is scheduler mechanics, not application behavior — the
                    // AGENTS.md effect-boundary invariant carves out "ambient runtime for executor mechanics".
                    StartedAt = clock.UtcNow()
                    StartedTick = clock.Elapsed()
                    SettledAt = None
                    SettledTick = None
                    Status = FiberStatus.Running
                }

            let tracker = FiberDefectTracker(metadata, observer)

            // Everything the fiber acquires belongs to its own scope, which closes when the fiber settles. An owned
            // fiber is supervised by the forking scope: the supervision finalizer registered below interrupts the
            // fiber and waits for that close, so the fiber scope itself is not also registered as a child (the
            // lazy-scope path for resource-free fibers; Scope allocates finalizer storage only on first use). An
            // explicitly unowned internal fiber is managed by the caller through its handle and registers with an
            // independent scope.
            let fiberScope = new Scope()

            let registrationScope =
                match owner with
                | ForkOwner.Caller -> parentRuntime.Scope
                | ForkOwner.Root -> parentRuntime.RootScope
                | ForkOwner.Unowned -> new Scope()

            // Copy the immutable inherited runtime once. Chaining record helpers rebuilt the full record twice for
            // every fork even though only the fiber identity and scope differ from the parent.
            let childRuntime =
                { parentRuntime with
                    FiberId = metadata.Id
                    Scope = fiberScope }

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
                | ValueSome key -> registrationScope.Unregister key
                | ValueNone -> ()

            // A graceful fiber is not tied to the forking flow's cancellation: its stop runs when the forking scope
            // closes, which also happens when the forking flow is interrupted.
            let parentToken =
                match graceful with
                | Some _ -> Platform.Cancellation.never
                | None -> RuntimeContext.forkToken parentRuntime cancellationToken

            let cts, exitTask =
                Platform.startFiber
                    parentToken
                    (fun status exit ->
                        settled.Value <- Some exit
                        FiberInterrupts.remove metadata.Id
                        metadata.SettledAt <- Some(clock.UtcNow())
                        metadata.SettledTick <- Some(clock.Elapsed())
                        metadata.Status <- status

                        let defect =
                            match exit with
                            | Exit.Success _ -> None
                            | Exit.Failure cause -> Cause.defects cause |> List.tryHead

                        tracker.Settled defect

                        if status = FiberStatus.Failed && not parentRuntime.Registries.IsEmpty then
                            match exit with
                            | Exit.Failure cause ->
                                let text = Cause.prettyPrint (fun error -> OutcomeText.plain (box error)) cause

                                for registry in parentRuntime.Registries do
                                    // axial-note AXG010: diagnostic history failure must not prevent settlement or replace the original fiber failure.
                                    try registry.RecordFailure metadata text with _ -> ()
                            | Exit.Success _ -> ()

                        // A defect keeps the registration so the scope's close can report it deterministically. A
                        // root-owned (disconnected) fiber's outcome is always consumed by its caller or reported by
                        // disconnect, so it never needs the scope for that and must not accumulate in the root scope.
                        if defect.IsNone || owner = ForkOwner.Root then
                            Platform.lock registrationGate (fun () ->
                                settledClean.Value <- true
                                releaseRegistration ())

                        FiberObserver.notifyEnd observer metadata defect)
                    (fun childToken ->
                        Platform.runScoped
                            fiberScope.Close
                            childToken
                            (fun () -> RuntimeState.withRuntime childRuntime (fun () -> invoke flow environment childToken))
                            (fun cleanupError executionError exit ->
                                combineCleanup cleanupError executionError exit "Forked flow execution produced no outcome."))

            // A fiber belongs to the scope that forked it. Closing that scope interrupts the fiber, or first asks a
            // graceful fiber to stop and waits up to its grace period, then waits for its cleanup and reports an
            // unobserved defect deterministically.
            try
#if FABLE_COMPILER
                let key =
                    registrationScope.Register(fun _ -> async {
                        let stopFailure = ref None

                        match graceful with
                        | Some request ->
                            let! stopExit = Platform.Cancellation.uninterruptible (invoke request.Stop environment)

                            match stopExit with
                            | Exit.Failure cause -> stopFailure.Value <- Cause.defects cause |> List.tryHead
                            | Exit.Success() -> ()

                            // Wait for the fiber or the grace period, whichever ends first, on the runtime's time.
                            let graceTimer = new CancellationTokenSource()

                            Platform.startImmediate
                                (async {
                                    let! _ = exitTask
                                    graceTimer.Cancel()
                                })
                                None

                            let! _ = time.Sleep<unit>(request.Grace, graceTimer.Token)
                            ()
                        | None -> ()

                        cts.Cancel()
                        let! _ = exitTask
                        tracker.TryReport()

                        // A stop request that failed is a cleanup defect of the closing scope.
                        match stopFailure.Value with
                        | Some error -> raise error
                        | None -> ()
                    })
#else
                let weakTracker = WeakReference<FiberDefectTracker>(tracker)

                let key =
                    registrationScope.Register(fun _ -> task {
                        let mutable stopFailure = None

                        match graceful with
                        | Some request ->
                            let! stopExit = (Platform.Cancellation.uninterruptible (invoke request.Stop environment)).AsTask()

                            match stopExit with
                            | Exit.Failure cause ->
                                stopFailure <- Cause.defects cause |> List.tryHead
                            | Exit.Success () -> ()

                            use graceTimer = new CancellationTokenSource()
                            let graceElapsed = (time.Sleep<unit>(request.Grace, graceTimer.Token)).AsTask()
                            let! _ = Task.WhenAny(exitTask :> Task, graceElapsed :> Task)
                            graceTimer.Cancel()
                        | None -> ()

                        cts.Cancel()
                        let! _ = exitTask

                        match weakTracker.TryGetTarget() with
                        | true, live -> live.TryReport()
                        | _ -> ()

                        // A stop request that failed is a cleanup defect of the closing scope.
                        match stopFailure with
                        | Some error -> raise error
                        | None -> ()
                    })
#endif
                Platform.lock registrationGate (fun () ->
                    registration.Value <- ValueSome key

                    if settledClean.Value then
                        releaseRegistration ())
            // axial-note AXG010: a closing scope can reject registration after startup; keep publishing the already started fiber's handle and interrupt registration.
            with _ -> ()

            // A fiber that settled before this line removed nothing, so it is removed again after registering.
            FiberInterrupts.register metadata.Id cts

            if settled.Value.IsSome then
                FiberInterrupts.remove metadata.Id

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
    /// <para>
    /// Forking turns a cold flow description into hot child work and returns a handle
    /// that can later be joined or interrupted. Prefer <c>zipPar</c> or <c>race</c>
    /// when the caller only needs a simple parallel composition. Wait for the handle with <c>Fiber.join</c> or
    /// <c>Fiber.awaitExit</c>, and stop it with <c>Fiber.interrupt</c>.
    /// </para>
    /// <para>
    /// The fiber runs in its own scope and releases its scoped resources (anything registered with a scope, such as
    /// <c>scopeResource</c>, <c>scopeFinalizer</c>, or a hub subscription) when it settles, whether or not anyone
    /// joins it. A value the fiber returns therefore outlives the scoped resources the fiber acquired: a resource
    /// returned through <c>Fiber.join</c> has already been released. Use <c>forkResource</c> to acquire a resource
    /// in the background and keep it, or <c>zipPar</c> and <c>sequencePar</c> to acquire several in parallel.
    /// </para>
    /// </remarks>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:Axial.Fiber`2" /> handle.</returns>
    let fork (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkWith ForkOwner.Caller None None flow

    /// <summary>Starts a flow in a new fiber carrying a diagnostic name.</summary>
    /// <remarks>
    /// The name appears in <c>FiberDump</c> snapshots, <c>FiberRegistry</c> dumps, and telemetry fiber spans,
    /// so long-lived background fibers are recognizable in diagnostics instead of showing as bare ids.
    /// </remarks>
    /// <param name="name">The diagnostic name recorded in the fiber's metadata.</param>
    /// <param name="flow">The flow to fork.</param>
    /// <returns>A flow that produces a <see cref="T:Axial.Fiber`2" /> handle.</returns>
    let forkNamed (name: string) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkWith ForkOwner.Caller (Some name) None flow

    /// Starts a fiber whose lifetime is explicitly managed by its caller instead of the caller's scope.
    let internal forkUnownedNamed (name: string) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkWith ForkOwner.Unowned (Some name) None flow

    /// Starts an observed, unowned fiber for internal supervisors that deliberately consume no public handle.
    let internal forkUnownedDetachedNamed
        (name: string)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            invoke (forkWith ForkOwner.Unowned (Some name) None flow) environment cancellationToken
            |> Execution.map (fun (fiber: Fiber<'error, 'value>) ->
                fiber.MarkObserved()
                fiber))

    /// <summary>
    /// Starts a flow in a new fiber that, when its forking scope closes, is asked to stop and given time to finish
    /// before it is interrupted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain <c>fork</c> is interrupted as soon as its scope closes. A consumer draining a queue needs the
    /// opposite: stop taking new work, finish the backlog, then end. When the forking scope closes,
    /// <c>forkGraceful</c> runs <paramref name="stop" />, waits up to <paramref name="grace" /> for the fiber to finish
    /// on its own, and only then interrupts it. For a queue consumer, <c>stop</c> is usually <c>Dequeue.shutdown</c>:
    /// the consumer's stream ends normally once it has drained the backlog.
    /// </para>
    /// <para>
    /// The fiber is not interrupted when the forking flow is interrupted, only when its scope closes after the stop
    /// and grace period. <c>Fiber.interrupt</c> still interrupts it immediately. A defect in <c>stop</c> is reported
    /// as a cleanup defect of the closing scope.
    /// </para>
    /// </remarks>
    /// <param name="stop">Asks the fiber to finish, for example by shutting down the queue it consumes.</param>
    /// <param name="grace">How long to wait after <paramref name="stop" /> before interrupting the fiber.</param>
    /// <param name="flow">The flow to fork.</param>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (samples: Queue&lt;float&gt;) = Queue.bounded 1000
    ///     let! _ =
    ///         samples
    ///         |&gt; FlowStream.fromDequeue
    ///         |&gt; FlowStream.runForEachFlow writeSample
    ///         |&gt; Flow.forkGraceful (Dequeue.shutdown samples) (TimeSpan.FromSeconds 5.0)
    ///     do! samples |&gt; Queue.offer 21.5 |&gt; Flow.ignore
    /// }
    /// |&gt; Flow.scoped // closing the scope flushes every offered sample before returning
    /// </code>
    /// </example>
    let forkGraceful
        (stop: Flow<'env, Never, unit>)
        (grace: TimeSpan)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'none, Fiber<'error, 'value>> =
        if grace < TimeSpan.Zero then
            invalidArg (nameof grace) "A grace period cannot be negative."

        forkWith ForkOwner.Caller None (Some { Stop = stop; Grace = grace }) flow

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

    /// <summary>Lets interruption stop waiting for a flow without waiting for that flow to settle.</summary>
    /// <remarks>
    /// <para>
    /// The source runs in its own fiber. If the caller is interrupted, <c>disconnect</c> promptly returns
    /// <c>Cause.Interrupt</c> while the source continues settling in its detached fiber, and its lifecycle observer
    /// receives the eventual outcome. An eventual defect is additionally reported through <c>OnUnobservedDefect</c>
    /// exactly once because no caller can consume it.
    /// </para>
    /// <para>
    /// The caller stops waiting, but the application does not: the detached fiber belongs to the root run's scope
    /// (<c>Flow.run</c>, <c>App.start</c>), which interrupts it and waits for it when the application finishes.
    /// </para>
    /// <para>
    /// This is an explicit escape hatch for foreign operations that may ignore interruption. Prefer structured
    /// combinators, which wait for child cleanup. At a bounded shutdown boundary, wait first and apply
    /// <c>disconnect</c> only to the forced-close operation whose cleanup may remain running.
    /// </para>
    /// </remarks>
    /// <param name="flow">The flow whose settlement may outlive an interrupted caller.</param>
    /// <returns>A flow with the source outcome unless the caller is interrupted first.</returns>
    let disconnect (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            invoke (forkWith ForkOwner.Root (Some "flow-disconnect") None flow) environment cancellationToken
            |> Execution.bind (fun (fiber: Fiber<'error, 'value>) ->
                let settled = Platform.newSignal<Exit<'value, 'error>> ()
                let gate = obj()
                let mutable disconnected = false

                Platform.observeExitTask fiber.ExitTask (fun exit ->
                    Platform.resolveSignal settled exit |> ignore

                    if Platform.lock gate (fun () -> disconnected) then
                        fiber.ReportUnobserved())

                Platform.awaitSignal settled cancellationToken
                |> Execution.fold
                    (fun exit ->
                        fiber.MarkObserved()
                        Execution.ofExit exit)
                    (fun cause ->
                        Platform.lock gate (fun () -> disconnected <- true)

                        match fiber.Settled with
                        | Some _ -> fiber.ReportUnobserved()
                        | None -> ()

                        Execution.ofCause cause)))

    /// <summary>Starts acquiring a resource in a new fiber, keeping the resource in the caller's scope.</summary>
    /// <remarks>
    /// <para>
    /// A fiber started with <c>fork</c> releases its scoped resources when it settles, so a resource it acquires is
    /// already released by the time <c>Fiber.join</c> returns it. <c>forkResource</c> runs only the acquisition in
    /// the fiber and registers the release with the caller's scope, so <c>Fiber.join</c> returns a live resource
    /// that is released when the caller's scope closes, as if the caller had used <c>scopeResource</c>.
    /// </para>
    /// <para>
    /// If the caller's scope closes before the acquisition finishes, the fiber is interrupted, and a resource that was
    /// acquired anyway is released straight away. Scoped resources that the acquiring flow itself registers along the
    /// way belong to the fiber and are released when it settles; only the described resource moves to the caller.
    /// To acquire several resources in parallel and use them together, use <c>zipPar</c> or <c>sequencePar</c>.
    /// </para>
    /// </remarks>
    /// <param name="resource">The acquisition and release description.</param>
    /// <returns>A flow that produces a fiber whose value is the acquired resource.</returns>
    /// <example>
    /// <code>
    /// flow {
    ///     let! connecting = Flow.forkResource databaseConnection
    ///     let! config = loadConfig
    ///     let! connection = Fiber.join connecting // still open
    ///     return! migrate connection config
    /// }
    /// |&gt; Flow.scoped // the connection is released here
    /// </code>
    /// </example>
    let forkResource (Resource(acquire, release)) : Flow<'env, 'none, Fiber<'error, 'value>> =
        Flow(fun environment cancellationToken ->
            let ownerScope = RuntimeState.current().Scope

            let acquireForOwner =
                Flow(fun environment cancellationToken ->
                    invoke acquire environment cancellationToken
                    |> Execution.bind (fun value ->
                        registerOrRelease ownerScope (releaseFinalizer environment release value) value))

            invoke (fork acquireForOwner) environment cancellationToken)

    // Starts a computation that several callers share. It runs once, as a detached fiber in `owner`'s scope and
    // with no caller's cancellation token, so a caller that is interrupted stops only its own wait; closing the
    // owner's scope interrupts the computation. `onSettled` runs before waiters are woken.
    let internal startShared
        (owner: RuntimeContext)
        (environment: 'env)
        (computation: Flow<'env, 'error, 'value>)
        (onSettled: Exit<'value, 'error> -> unit)
        : Platform.Signal<Exit<'value, 'error>> * (unit -> Execution<unit, 'none>) =
        let signal = Platform.newSignal ()

        let settle exit =
            onSettled exit
            Platform.resolveSignal signal exit |> Operators.ignore
            Execution.ofValue ()

        let publisher : Flow<'env, Never, unit> =
            Flow(fun environment cancellationToken ->
                invoke computation environment cancellationToken
                |> Execution.fold (fun value -> settle (Exit.Success value)) (fun cause -> settle (Exit.Failure cause)))

        let start () =
            RuntimeState.withRuntime owner (fun () ->
                invoke (forkDetached publisher) environment Platform.Cancellation.never)
            |> Execution.map (fun (_: Fiber<Never, unit>) -> ())

        signal, start

    // Waits for a shared computation. Interrupting the waiter ends this wait only.
    let internal awaitShared
        (signal: Platform.Signal<Exit<'value, 'error>>)
        (cancellationToken: CancellationToken)
        : Execution<'value, 'error> =
        Execution.fold Execution.ofExit Execution.ofCause (Platform.awaitSignal signal cancellationToken)

    /// <summary>Returns a flow that runs <paramref name="flow" /> at most once at a time and remembers its value.</summary>
    /// <remarks>
    /// <para>
    /// The first caller of the returned flow starts the computation; callers that arrive while it runs wait for the
    /// same result instead of starting another. A success is remembered for every later caller. A failure is not:
    /// the next caller starts a fresh attempt.
    /// </para>
    /// <para>
    /// The computation runs in the scope and environment where <c>memoize</c> ran, not in any caller's, so
    /// interrupting one caller never cancels the computation the others are waiting for. Closing that scope
    /// interrupts it.
    /// </para>
    /// </remarks>
    /// <param name="flow">The computation to share.</param>
    /// <returns>A flow that produces the memoized flow.</returns>
    /// <example>
    /// <code>
    /// let readConfig : Flow&lt;unit, string, string&gt; = Flow.delay (fun () -&gt; Flow.ok "config")
    ///
    /// let sameConfig : Flow&lt;unit, string, bool&gt; =
    ///     flow {
    ///         let! loadConfig = Flow.memoize readConfig
    ///         let! a = loadConfig
    ///         let! b = loadConfig // readConfig ran once
    ///         return a = b
    ///     }
    /// </code>
    /// </example>
    let memoize (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Flow<'caller, 'error, 'value>> =
        Flow(fun environment _ ->
            let owner = RuntimeState.current()
            let gate = obj()
            let current : Platform.Signal<Exit<'value, 'error>> option ref = ref None

            let shared =
                Flow(fun _ cancellationToken ->
                    let signal, start =
                        Platform.lock gate (fun () ->
                            match current.Value with
                            | Some signal -> signal, None
                            | None ->
                                // The computation starts only after this lock is released, so `mine` is set
                                // before `forget` can run.
                                let mine = ref None

                                let forget exit =
                                    match exit with
                                    | Exit.Failure _ ->
                                        Platform.lock gate (fun () ->
                                            match current.Value, mine.Value with
                                            | Some active, Some own when obj.ReferenceEquals(active, own) -> current.Value <- None
                                            | _ -> ())
                                    | Exit.Success _ -> ()

                                let signal, start = startShared owner environment flow forget
                                mine.Value <- Some signal
                                current.Value <- Some signal
                                signal, Some start)

                    match start with
                    | Some start -> start () |> Execution.bind (fun () -> awaitShared signal cancellationToken)
                    | None -> awaitShared signal cancellationToken)

            Execution.ofValue shared)

    // Runs `flow` only after the fiber it replaces has settled, so a replacement never overlaps the cleanup of the
    // work it replaced. The wait ignores the replacement's own interruption: a replacement superseded while waiting
    // still waits, which keeps a chain of rapid replacements serialised. A replaced fiber's defect is reported once
    // through the fiber observer unless someone else consumed its outcome.
    let private afterReplaced (previous: Platform.Signal<Fiber<'error, 'value> option>) (flow: Flow<'env, 'error, 'value>) =
        Flow(fun environment cancellationToken ->
            Platform.awaitSignal previous Platform.Cancellation.never
            |> Execution.bind (fun (replaced: Fiber<'error, 'value> option) ->
                match replaced with
                | Some fiber ->
                    fiber.InterruptSource.Cancel()

                    Platform.awaitExitTaskAsSuccess fiber.ExitTask
                    |> Execution.map (fun _ -> fiber.ReportUnobserved())
                | None -> Execution.ofValue ())
            |> Execution.bind (fun () ->
                if cancellationToken.IsCancellationRequested then
                    Execution.ofCause Cause.Interrupt
                else
                    invoke flow environment cancellationToken))

    // Forks the replacement first so its handle can enter the slot atomically, then tells it which fiber it replaced.
    let private forkReplacingWith (replace: Fiber<'error, 'value> -> Fiber<'error, 'value> option) flow =
        Flow(fun environment cancellationToken ->
            let previous = Platform.newSignal<Fiber<'error, 'value> option> ()

            invoke (fork (afterReplaced previous flow)) environment cancellationToken
            |> Execution.map (fun fiber ->
                Platform.resolveSignal previous (replace fiber) |> ignore
                fiber))

    /// <summary>Starts a flow in a new fiber held by <paramref name="slot" />, interrupting the fiber it replaces.</summary>
    /// <remarks>
    /// <para>
    /// Latest wins: the previous fiber in the slot is interrupted, and the new flow starts only after the previous
    /// fiber has settled and released its scoped resources, so the two never overlap. A replacement that is itself
    /// replaced before it starts never runs. Use this for requests where a result for stale input is worthless, such
    /// as search-as-you-type.
    /// </para>
    /// <para>
    /// A replaced fiber's outcome is not returned to anyone. A defect it settles with is reported through the
    /// fiber observer exactly once, unless the caller consumed it with <c>Fiber.join</c> or <c>Fiber.awaitExit</c>.
    /// </para>
    /// </remarks>
    /// <param name="slot">The slot that holds the current fiber.</param>
    /// <param name="flow">The flow to run.</param>
    /// <returns>A flow that produces the new fiber's handle.</returns>
    /// <example>
    /// <code>
    /// let onQueryChanged slot query =
    ///     search query |&gt; Flow.forkReplacing slot |&gt; Flow.ignore
    /// </code>
    /// </example>
    let forkReplacing (slot: FiberSlot<'error, 'value>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkReplacingWith slot.Replace flow

    /// <summary>Starts a flow in a new fiber held under <paramref name="key" />, interrupting that key's previous fiber.</summary>
    /// <remarks>
    /// Like <c>forkReplacing</c>, per key: the new flow starts after that key's previous fiber has settled, while
    /// fibers for other keys keep running. A key's entry is removed when its fiber settles, so the slot does not grow
    /// with every key ever used.
    /// </remarks>
    /// <param name="key">The key whose previous fiber is replaced.</param>
    /// <param name="slots">The keyed slot that holds the current fibers.</param>
    /// <param name="flow">The flow to run.</param>
    /// <returns>A flow that produces the new fiber's handle.</returns>
    /// <example>
    /// <code>
    /// loadPreview document |&gt; Flow.forkReplacingKey document.Id previews
    /// </code>
    /// </example>
    let forkReplacingKey
        (key: 'key)
        (slots: KeyedFiberSlot<'key, 'error, 'value>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'none, Fiber<'error, 'value>> =
        forkReplacingWith
            (fun fiber ->
                let marker = obj ()
                let previous = slots.Replace(key, marker, fiber)
                // Registered only after the fiber is recorded, so a fiber that settles at once still removes itself.
                Platform.whenSettled fiber.ExitTask (fun () -> slots.Remove(key, marker))
                previous)
            flow

    /// <summary>Combines two flows into a tuple of their values, running them concurrently.</summary>
    /// <remarks>
    /// <para>If either flow fails, the other is interrupted immediately.</para>
    /// <para>Branches start inline, like async methods passed to <c>Task.WhenAll</c>; use <c>Flow.runOn RunOn.ThreadPool</c> for synchronous CPU-heavy branches.</para>
    /// <para>
    /// The two flows share a scope of their own. When both succeed, the scoped resources they acquired move to the
    /// caller's scope, as if the caller had acquired them, so <c>zipPar (scopeResource a) (scopeResource b)</c>
    /// acquires two resources in parallel and both stay open until the caller's scope closes. When <c>zipPar</c> fails,
    /// both flows' scoped resources are released before it returns.
    /// </para>
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
        // Both flows share one branch scope: the caller receives both values or neither, so their scoped resources
        // are kept or released together.
        parallelBranch (fun source environment ->
            Platform.zipParExecution (invoke left environment) (invoke right environment) source chooseParallelExit)

    /// <summary>Runs all flows concurrently and returns their values in input order.</summary>
    /// <remarks>An empty input succeeds immediately. Branches start inline, like async methods passed to <c>Task.WhenAll</c>; use <c>Flow.runOn RunOn.ThreadPool</c> for synchronous CPU-heavy branches. If any flow fails, remaining flows are interrupted through the same structured parallel composition as <c>zipPar</c>. Scoped resources follow <c>zipPar</c> too: when every flow succeeds they move to the caller's scope; when any fails, all of them are released before <c>sequencePar</c> returns.</remarks>
    let sequencePar (flows: Flow<'env, 'error, 'value> list) : Flow<'env, 'error, 'value list> =
        match flows with
        | [] -> ok []
        | _ ->
            // Write leaves directly into the result array. The former tree returned and concatenated a list at every
            // node, allocating each successful value once per tree level.
            let inputs = List.toArray flows

            let rec combine first count
                : CancellationTokenSource -> 'env -> CancellationToken -> 'value array -> Execution<unit, 'error> =
                if count = 1 then
                    fun _ environment token results ->
                        invoke inputs[first] environment token
                        |> Execution.map (fun value -> results[first] <- value)
                else
                    let leftCount = count / 2
                    let startLeft = combine first leftCount
                    let startRight = combine (first + leftCount) (count - leftCount)

                    fun source environment _ results ->
                        Platform.zipParUnitExecution
                            (fun token -> startLeft source environment token results)
                            (fun token -> startRight source environment token results)
                            source
                            chooseParallelExit

            let start = combine 0 inputs.Length

            parallelBranch (fun source environment ->
                let results = Array.zeroCreate inputs.Length
                start source environment source.Token results
                |> Execution.map (fun () -> List.ofArray results))

    /// <summary>Runs two flows concurrently and returns the result of the first one to complete.</summary>
    /// <remarks>
    /// <para>The "loser" flow is interrupted, awaited, and its scope is closed before <c>race</c> returns.</para>
    /// <para>
    /// Each flow runs in its own scope. If the winner succeeds, its scoped resources move to the caller's scope, so
    /// racing two connection attempts keeps the connection that won. A defect from the loser or its cleanup is
    /// combined with a failing winner, or becomes the race failure when the winner succeeded. Typed loser failures
    /// and the interruption requested by <c>race</c> do not replace the winner's result.
    /// </para>
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
            let parent = RuntimeState.current()
            let struct (leftScope, leftOp) = branchIn parent cancellationToken left environment
            let struct (rightScope, rightOp) = branchIn parent cancellationToken right environment
            let leftWon = ref false
            let loserExit = ref None
            let winnerScope () = if leftWon.Value then leftScope else rightScope
            let loserScope () = if leftWon.Value then rightScope else leftScope

            let finishWinner (winnerExit: Exit<'value, 'error>) : Execution<'value, 'error> =
                match winnerExit with
                | Exit.Success value ->
                    keepBranch (winnerScope ())
                    Execution.ofValue value
                | Exit.Failure _ -> closeRaceScope cancellationToken (winnerScope ()) winnerExit

            let finishLoser (winnerExit: Exit<'value, 'error>) (loserOutcome: Exit<unit, 'error>) : Execution<'value, 'error> =
                match loserOutcome with
                | Exit.Success () -> finishWinner winnerExit
                | Exit.Failure loserCause ->
                    match Cause.defects loserCause with
                    | [] -> finishWinner winnerExit
                    | defects ->
                        let defectCause = defects |> List.map Cause.Die |> List.reduce Cause.both

                        match winnerExit with
                        | Exit.Success _ -> finishWinner (Exit.Failure defectCause)
                        | Exit.Failure winnerCause ->
                            finishWinner (Exit.Failure(Cause.both winnerCause defectCause))

            let settle (winnerExit: Exit<'value, 'error>) : Execution<'value, 'error> =
                let loserOutcome =
                    match loserExit.Value with
                    | Some(Exit.Failure cause) -> Exit.Failure cause
                    | _ -> Exit.Success()

                let closing = Platform.Cancellation.uninterruptible (fun token ->
                    closeRaceScope token (loserScope ()) loserOutcome)
#if !FABLE_COMPILER
                if closing.IsCompletedSuccessfully then
                    Platform.guardStack (fun () -> finishLoser winnerExit closing.Result)
                else
                    closing
                    |> Execution.fold
                        (fun () -> finishLoser winnerExit (Exit.Success ()))
                        (fun cause -> finishLoser winnerExit (Exit.Failure cause))
#else
                closing
                |> Execution.fold
                    (fun () -> finishLoser winnerExit (Exit.Success ()))
                    (fun cause -> finishLoser winnerExit (Exit.Failure cause))
#endif

            let settled =
                Platform.raceExecution
                    leftOp
                    rightOp
                    cancellationToken
                    (fun won -> leftWon.Value <- won)
                    (function
                        | Ok exit -> loserExit.Value <- exit
                        | Error error -> reportUnobservedCause (Cause.Die error))
#if !FABLE_COMPILER
            if settled.IsCompletedSuccessfully then
                Platform.guardStack (fun () -> settle settled.Result)
            else
                settled |> Execution.fold (Exit.Success >> settle) (Exit.Failure >> settle)
#else
            settled |> Execution.fold (Exit.Success >> settle) (Exit.Failure >> settle)
#endif
            )

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

    /// <summary>Attaches an environment-derived error to a result that failed without one; the error flow runs only on failure.</summary>
    /// <remarks>
    /// <para>
    /// The <c>unit</c> error means the absence of a reason. <c>Result.okIf</c> and
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
    /// let flow = Flow.orElseFlow (Flow.envWith (fun env -> "error")) result
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
    /// On .NET, a recursive <c>return!</c> through <c>bind</c> does not keep a task per call alive. Work still pending
    /// in the source, such as a <c>use</c> or <c>try/finally</c>, lives until the source completes.
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
#if FABLE_COMPILER
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.bind (fun value -> invoke (binder value) environment cancellationToken))
#else
        FlowContinue(fun environment cancellationToken ->
            let execution = invoke flow environment cancellationToken

            if execution.IsCompletedSuccessfully then
                match execution.Result with
                | Exit.Success value -> FlowStep.Next(binder value)
                | Exit.Failure cause -> FlowStep.Failed cause
            else
                FlowStep.Await(BindAwaiter(execution.AsTask(), binder)))
#endif

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
                    match Cause.untraced cause with
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

    /// <summary>Gives a flow that cannot fail with a typed error any error type.</summary>
    /// <remarks>
    /// A <c>Flow&lt;'env, Never, 'value&gt;</c> has no typed failures, so it can stand wherever a flow with a real error
    /// type is expected. Defects and interruption pass through unchanged.
    /// </remarks>
    /// <example>
    /// <code>
    /// let ready : Flow&lt;unit, string, int&gt; = Flow.succeed 1 |> Flow.widenError
    /// </code>
    /// </example>
    let widenError (flow: Flow<'env, Never, 'value>) : Flow<'env, 'error, 'value> = mapError Never.absurd flow

    /// <summary>Attaches diagnostic trace text to any failure cause of the flow.</summary>
    /// <remarks>
    /// On failure the cause is wrapped in <c>Cause.Traced</c>, so retries, parallel composition, and
    /// telemetry (<c>Cause.prettyPrint</c>, the <c>axial.flow.cause</c> span tag) can show where in the
    /// workflow the failure passed through. Successful values are untouched, and the typed error is not
    /// changed; only the cause tree gains a trace node.
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

    /// <summary>Folds both the successful value and the full failure cause into a new flow.</summary>
    /// <remarks>
    /// <para>
    /// An expert escape hatch. The failure handler sees typed failures, defects, and interruption, and whatever flow
    /// it returns replaces them: a handler that returns success swallows an interruption (the flow keeps running
    /// after its owner asked it to stop) or a defect (a bug disappears). Unless the caller owns the cancellation, the
    /// handler must re-raise <c>Cause.Interrupt</c>, and it should re-raise or report every <c>Cause.Die</c>.
    /// </para>
    /// <para>For typed errors only, use <c>Flow.fold</c>, <c>Flow.recover</c>, or <c>Flow.orElseWith</c>, which never see interruption or defects.</para>
    /// </remarks>
    /// <param name="onSuccess">A function that returns a new flow from the success value.</param>
    /// <param name="onFailure">A function that returns a new flow from the failure cause.</param>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow that continues with the outcome of either <paramref name="onSuccess" /> or <paramref name="onFailure" />.</returns>
    let foldCause
        (onSuccess: 'value -> Flow<'env, 'nextError, 'next>)
        (onFailure: Cause<'error> -> Flow<'env, 'nextError, 'next>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'next> =
        Flow(fun environment cancellationToken ->
            invoke flow environment cancellationToken
            |> Execution.fold
                (fun value -> invoke (onSuccess value) environment cancellationToken)
                (fun cause -> invoke (onFailure cause) environment cancellationToken))

    // A Never-typed cause has no Fail case, so its defects and interruptions carry over to any error type.
    let rec private widenCause (cause: Cause<Never>) : Cause<'error> =
        match cause with
        | Cause.Fail _ -> Cause.Die(InvalidOperationException "A Never-typed flow cannot fail with a typed error.")
        | Cause.Die error -> Cause.Die error
        | Cause.Interrupt -> Cause.Interrupt
        | Cause.Then(left, right) -> Cause.Then(widenCause left, widenCause right)
        | Cause.Both(left, right) -> Cause.Both(widenCause left, widenCause right)
        | Cause.Traced(inner, trace) -> Cause.Traced(widenCause inner, trace)

    /// <summary>Runs a handler with the flow's final outcome, whether it succeeded, failed, or was interrupted.</summary>
    /// <remarks>
    /// <para>
    /// The handler runs after the flow finishes and before the result is returned. It runs without the flow's
    /// cancellation, so the interruption that ended the flow cannot cut its cleanup short. The flow's outcome is kept:
    /// a defect in the handler is added to a failure, and turns a success into that defect.
    /// </para>
    /// <para>
    /// Use it for cleanup that belongs to one expression and needs to know how it ended, such as recording the
    /// outcome or rolling back on failure. For cleanup tied to a resource's lifetime, use <c>Flow.scopeAcquireRelease</c>.
    /// </para>
    /// </remarks>
    /// <param name="handler">Receives the flow's <c>Exit</c>; it cannot fail with a typed error.</param>
    /// <param name="flow">The source flow.</param>
    /// <example>
    /// <code>
    /// transfer
    /// |&gt; Flow.onExit (fun exit -&gt;
    ///     match exit with
    ///     | Exit.Success _ -&gt; audit "transfer committed"
    ///     | Exit.Failure _ -&gt; audit "transfer rolled back")
    /// </code>
    /// </example>
    let onExit
        (handler: Exit<'value, 'error> -> Flow<'env, Never, unit>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            let finish (exit: Exit<'value, 'error>) : Execution<'value, 'error> =
                Platform.tryExecution
                    (fun () -> Platform.Cancellation.uninterruptible (invoke (handler exit) environment))
                    (fun error -> Platform.ofExit (Exit.Failure(Cause.Die error)))
                |> Execution.fold
                    (fun () -> Execution.ofExit exit)
                    (fun handlerCause ->
                        match exit with
                        | Exit.Success _ -> Execution.ofCause (widenCause handlerCause)
                        | Exit.Failure cause -> Execution.ofCause (Cause.thenCause cause (widenCause handlerCause)))

            Platform.tryExecution
                (fun () -> invoke flow environment cancellationToken)
                (fun error ->
                    if ForeignCancellation.isOurs cancellationToken error then
                        Platform.ofExit (Exit.Failure Cause.Interrupt)
                    else
                        Platform.ofExit (Exit.Failure(Cause.Die error)))
            |> Execution.fold (fun value -> finish (Exit.Success value)) (fun cause -> finish (Exit.Failure cause)))

    /// <summary>Runs a finalizer after the flow, however it ends.</summary>
    /// <remarks>
    /// The finalizer runs after success, typed failure, defect, and interruption alike, without the flow's
    /// cancellation, so it always finishes. The flow's outcome is kept; a defect in the finalizer is added to it.
    /// </remarks>
    /// <param name="finalizer">The cleanup to run; it cannot fail with a typed error.</param>
    /// <param name="flow">The source flow.</param>
    /// <example>
    /// <code>
    /// render frame |&gt; Flow.ensuring (Flow.delay (fun () -&gt; canvas.Unlock(); Flow.ok ()))
    /// </code>
    /// </example>
    let ensuring (finalizer: Flow<'env, Never, unit>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        onExit (fun _ -> finalizer) flow

    /// <summary>Runs a handler only if the flow is interrupted.</summary>
    /// <remarks>
    /// The handler runs when the flow's outcome includes an interruption, before the interruption propagates, and
    /// without the flow's cancellation, so it always finishes. Success, typed failure, and defects skip it.
    /// </remarks>
    /// <param name="handler">The cleanup to run on interruption; it cannot fail with a typed error.</param>
    /// <param name="flow">The source flow.</param>
    /// <example>
    /// <code>
    /// upload |&gt; Flow.onInterrupt (deletePartialFile path)
    /// </code>
    /// </example>
    let onInterrupt (handler: Flow<'env, Never, unit>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        onExit
            (fun exit ->
                match exit with
                | Exit.Failure cause when Cause.isInterrupted cause -> handler
                | _ -> Flow(fun _ _ -> Execution.ofValue ()))
            flow

    /// <summary>A flow that never completes on its own; it ends only when it is interrupted.</summary>
    /// <remarks>
    /// Use it to keep a flow running until its fiber or scope is interrupted, such as a service's main loop that
    /// only waits for shutdown. It holds no thread while it waits.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Runs the worker until the service is interrupted, then lets it drain its queue.
    /// let serve (jobs: Queue&lt;string&gt;) (worker: Flow&lt;unit, Never, unit&gt;) : Flow&lt;unit, Never, unit&gt; =
    ///     flow {
    ///         let! _ = worker |&gt; Flow.forkGraceful (Dequeue.shutdown jobs) (TimeSpan.FromSeconds 5.0)
    ///         return! Flow.never
    ///     }
    /// </code>
    /// </example>
    let never<'env, 'error, 'value> : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken -> Platform.awaitSignal (Platform.newSignal<'value> ()) cancellationToken)

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
                            match Cause.untraced cause with
                            | Cause.Die error -> Execution.ofCause (Cause.Fail(handler error))
                            | _ -> Execution.ofCause cause))
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
                    match Cause.untraced cause with
                    | Cause.Fail error -> invoke (fallback error) environment cancellationToken
                    | _ -> Execution.ofCause cause))

    /// <summary>Recovers a typed failure with a flow whose error type may differ.</summary>
    /// <remarks>
    /// Like <c>orElseWith</c>, this handles a single <c>Cause.Fail</c>, including traced failures;
    /// unlike <c>orElseWith</c>, the handler may change the error type. <c>foldCause</c> handles every
    /// cause, including defects and interruption. Compound causes are not recovered. Because a
    /// compound cause may contain old typed errors that cannot inhabit the new error channel,
    /// each old typed failure becomes a <c>Cause.Die</c> containing an <c>UnrecoveredFailureException</c>
    /// whose <c>Value</c> holds the original failure; existing defects, interruption, and structure remain.
    /// If the handler throws, its exception is a defect, except cancellation requested by this flow's
    /// token, which is an interruption.
    /// </remarks>
    /// <example>
    /// <code>
    /// let optionalSettings (load: Flow&lt;'env, FileSystemError, Settings&gt;)
    ///     : Flow&lt;'env, Never, Settings option&gt; =
    ///     load |&gt; Flow.map Some |&gt; Flow.recover (fun _ -&gt; Flow.ok None)
    /// </code>
    /// </example>
    let private retypeUnrecovered (cause: Cause<'error>) : Cause<'nextError> =
        let pending = System.Collections.Generic.Stack<Choice<Cause<'error>, int * string>>()
        let results = System.Collections.Generic.Stack<Cause<'nextError>>()
        pending.Push(Choice1Of2 cause)
        while pending.Count > 0 do
            match pending.Pop() with
            | Choice1Of2 node ->
                match node with
                | Cause.Fail error -> results.Push(Cause.Die(UnrecoveredFailureException(box error)))
                | Cause.Die error -> results.Push(Cause.Die error)
                | Cause.Interrupt -> results.Push Cause.Interrupt
                | Cause.Then(left, right) ->
                    pending.Push(Choice2Of2(0, ""))
                    pending.Push(Choice1Of2 right)
                    pending.Push(Choice1Of2 left)
                | Cause.Both(left, right) ->
                    pending.Push(Choice2Of2(1, ""))
                    pending.Push(Choice1Of2 right)
                    pending.Push(Choice1Of2 left)
                | Cause.Traced(inner, trace) ->
                    pending.Push(Choice2Of2(2, trace))
                    pending.Push(Choice1Of2 inner)
            | Choice2Of2(kind, trace) ->
                if kind = 2 then results.Push(Cause.Traced(results.Pop(), trace))
                else
                    let right = results.Pop()
                    let left = results.Pop()
                    results.Push(if kind = 0 then Cause.Then(left, right) else Cause.Both(left, right))
        results.Pop()

    let recover
        (handler: 'error -> Flow<'env, 'nextError, 'value>)
        (source: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'value> =
        Flow(fun environment cancellationToken ->
            invoke source environment cancellationToken
            |> Execution.fold
                Execution.ofValue
                (fun cause ->
                    match Cause.untraced cause with
                    | Cause.Fail error ->
                        try invoke (handler error) environment cancellationToken
                        with thrown -> Execution.ofCause (ForeignCancellation.causeOf cancellationToken thrown)
                    | _ ->
                        Execution.ofCause (retypeUnrecovered cause)))

    /// <summary>Folds a successful value or a single typed failure into a new flow.</summary>
    /// <remarks>
    /// Traced typed failures reach <paramref name="onError" />. Defects and interruption pass through, and compound
    /// causes are retyped without recovery in the same way as <c>recover</c>.
    /// </remarks>
    /// <param name="onSuccess">A function that returns a new flow from the success value.</param>
    /// <param name="onError">A function that returns a new flow from the typed error.</param>
    /// <param name="source">The source flow.</param>
    /// <returns>A flow that continues with the success or typed-error result.</returns>
    let fold
        (onSuccess: 'value -> Flow<'env, 'nextError, 'next>)
        (onError: 'error -> Flow<'env, 'nextError, 'next>)
        (source: Flow<'env, 'error, 'value>)
        : Flow<'env, 'nextError, 'next> =
        source
        |> map Choice1Of2
        |> recover (fun error -> onError error |> map Choice2Of2)
        |> bind (function
            | Choice1Of2 value -> onSuccess value
            | Choice2Of2 next -> Flow(fun _ _ -> Execution.ofValue next))

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
    /// Internal seam for layers. The provisioning step is supplied as a plain function so
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
    /// <remarks>Recursive calls deferred here are consumed by the executor without growing a chain of parent tasks.
    /// Work that must run after the recursive call still needs storage until that call finishes.</remarks>
    /// <returns>A flow that lazily evaluates the factory when executed.</returns>
    /// <example>
    /// <code>
    /// let flow = Flow.delay (fun () -> Flow.succeed 42)
    /// </code>
    /// </example>
    let delay (factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
#if FABLE_COMPILER
        Flow(fun environment ct -> invoke (factory ()) environment ct)
#else
        FlowDelay factory
#endif

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

    // Runs a mapping over `items` with at most `parallelism` workers, each taking the next index from a shared
    // counter so a slow item delays only its own worker. Workers are combined with sequencePar, which runs each as a
    // branch and interrupts the others as soon as one fails, so what the items acquire is treated as in sequencePar:
    // kept by the caller when every item succeeds, released before the traversal returns otherwise.
    // `workerMapping` gives each worker its mapping. When `workerOwnsScope` is set it may acquire a per-worker
    // resource, so the worker opens a scope for it that closes when the worker finishes, and runs its items under
    // the runtime outside that scope so that what they acquire outlives the worker's resource.
    // `store` records each result; the counter and storage are allocated per run.
    let private runWorkers
        (parallelism: Parallelism)
        (workerMapping: Flow<'env, 'error, 'value -> Flow<'env, 'error, 'next>>)
        (workerOwnsScope: bool)
        (items: 'value array)
        (store: int -> 'next -> unit)
        : Flow<'env, 'error, unit> =
        Flow(fun environment cancellationToken ->
            let next = ref -1L
            let placement = Parallelism.placement parallelism

            // A loop rather than recursion, so a worker that processes many items runs in constant memory.
            let drain
                (mapping: 'value -> Flow<'env, 'error, 'next>)
                (itemRuntime: RuntimeContext voption)
                (cancellationToken: CancellationToken)
                =
                Execution.loop () (fun () ->
                    let index = int (Platform.nextId next)

                    if index >= items.Length then
                        Execution.ofValue (Platform.Break())
                    else
                        let item = mapping items[index] |> runOn placement

                        let running =
                            match itemRuntime with
                            | ValueSome runtime ->
                                RuntimeState.withRuntime runtime (fun () -> invoke item environment cancellationToken)
                            | ValueNone -> invoke item environment cancellationToken

                        running
                        |> Execution.map (fun mapped ->
                            store index mapped
                            Platform.Continue()))

            let worker =
                if workerOwnsScope then
                    Flow(fun environment cancellationToken ->
                        let outside = RuntimeState.current()

                        let withWorkerResource =
                            Flow(fun environment cancellationToken ->
                                invoke workerMapping environment cancellationToken
                                |> Execution.bind (fun mapping -> drain mapping (ValueSome outside) cancellationToken))
                            |> scoped

                        invoke withWorkerResource environment cancellationToken)
                else
                    Flow(fun environment cancellationToken ->
                        invoke workerMapping environment cancellationToken
                        |> Execution.bind (fun mapping -> drain mapping ValueNone cancellationToken))

            let workers = min (Parallelism.value parallelism) items.Length

            if items.Length > 0 && not workerOwnsScope && workers = items.Length then
                // With one worker per item there is no queue to drain. Start mapped items as leaves of the parallel
                // tree directly, avoiding a worker flow, loop, mapped Flow wrapper, and unit list per item.
                invoke workerMapping environment cancellationToken
                |> Execution.bind (fun mapping ->
                    let rec combine first count source token =
                        if count = 1 then
                            let item = mapping items[first] |> runOn placement
                            invoke item environment token
                            |> Execution.map (fun mapped -> store first mapped)
                        else
                            let leftCount = count / 2

                            Platform.zipParUnitExecution
                                (combine first leftCount source)
                                (combine (first + leftCount) (count - leftCount) source)
                                source
                                chooseParallelExit

                    invoke
                        (parallelBranch (fun source _ -> combine 0 items.Length source source.Token))
                        environment
                        cancellationToken)
            else
                invoke (sequencePar (List.replicate workers worker)) environment cancellationToken
                |> Execution.map (fun (_: unit list) -> ()))

    let private traverseWorkers
        parallelism
        workerMapping
        workerOwnsScope
        (values: seq<'value>)
        : Flow<'env, 'error, 'next list> =
        Flow(fun environment cancellationToken ->
            let items = Array.ofSeq values
            let results = Array.zeroCreate items.Length

            let store index mapped = results[index] <- mapped

            invoke (runWorkers parallelism workerMapping workerOwnsScope items store) environment cancellationToken
            |> Execution.map (fun () -> List.ofArray results))

    /// <summary>Maps values to flows and runs them with bounded concurrency, returning results in input order.</summary>
    /// <remarks>
    /// At most <paramref name="parallelism" /> mappings run at once; as each finishes, its worker starts the next
    /// value. The policy places each mapping; use <c>Parallelism.cpu</c> for synchronous CPU-heavy work. The first
    /// failure interrupts the mappings still running and waits for their cleanup before the flow fails, so no sibling
    /// is left running in the background. When each worker needs its own connection or handle, use
    /// <c>traverseParUsing</c>.
    /// Scoped resources follow <c>sequencePar</c>: when every mapping succeeds, what each one acquired moves to the
    /// caller's scope; when any fails, all of it is released before the traversal returns.
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
        traverseWorkers parallelism (ok mapping) false values

    /// <summary>Runs flows with bounded concurrency and the placement carried by <paramref name="parallelism"/>.</summary>
    /// <remarks>Results preserve input order. Failure, interruption, and scoped resources behave as in <c>sequencePar</c>.</remarks>
    let sequenceParWith
        (parallelism: Parallelism)
        (flows: seq<Flow<'env, 'error, 'value>>)
        : Flow<'env, 'error, 'value list> =
        traversePar parallelism id flows

    /// <summary>Like <c>traversePar</c>, giving each worker its own resource, acquired once and reused for every value it maps.</summary>
    /// <remarks>
    /// Each worker acquires <paramref name="resource" /> when it starts and releases it when it has no values left,
    /// or when the traversal fails or is interrupted. At most <paramref name="parallelism" /> resources exist at
    /// once. Use it for work that needs an expensive handle per thread of work, such as a database connection or a
    /// repository reader that is not safe to share. The policy places each mapping; use <c>Parallelism.cpu</c> for
    /// synchronous CPU-heavy work. The worker's resource never reaches the caller; other
    /// scoped resources the mapping acquires behave as in <c>traversePar</c>.
    /// </remarks>
    /// <param name="parallelism">The maximum number of workers, and so of resources.</param>
    /// <param name="resource">The resource each worker acquires.</param>
    /// <param name="mapping">Maps a value to a flow, given the worker's resource.</param>
    /// <param name="values">The values to map.</param>
    /// <returns>A flow containing the mapped values in the order of <paramref name="values" />.</returns>
    /// <example>
    /// <code>
    /// let! matches = commits |&gt; Flow.traverseParUsing (Parallelism.ofProcessors id) openReader (fun reader commit -&gt; search reader commit)
    /// </code>
    /// </example>
    let traverseParUsing
        (parallelism: Parallelism)
        (resource: Resource<'env, 'error, 'resource>)
        (mapping: 'resource -> 'value -> Flow<'env, 'error, 'next>)
        (values: seq<'value>)
        : Flow<'env, 'error, 'next list> =
        traverseWorkers parallelism (scopeResource resource |> map mapping) true values

    /// <summary>Runs a flow for each value with bounded concurrency, discarding the results.</summary>
    /// <remarks>Runs like <c>traversePar</c>: at most <paramref name="parallelism" /> at once, the first failure interrupts the rest, and scoped resources move to the caller's scope only when every flow succeeds. The policy places each action.</remarks>
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
            let run = runWorkers parallelism (ok action) false (Array.ofSeq values) (fun _ () -> ())
            invoke run environment cancellationToken)

    /// <summary>Like <c>forEachPar</c>, giving each worker its own resource, acquired once and reused for every value it processes.</summary>
    /// <remarks>Resources are acquired and released as in <c>traverseParUsing</c>. The policy places each action.</remarks>
    /// <param name="parallelism">The maximum number of workers, and so of resources.</param>
    /// <param name="resource">The resource each worker acquires.</param>
    /// <param name="action">The flow to run for each value, given the worker's resource.</param>
    /// <param name="values">The values to process.</param>
    let forEachParUsing
        (parallelism: Parallelism)
        (resource: Resource<'env, 'error, 'resource>)
        (action: 'resource -> 'value -> Flow<'env, 'error, unit>)
        (values: seq<'value>)
        : Flow<'env, 'error, unit> =
        Flow(fun environment cancellationToken ->
            invoke
                (runWorkers parallelism (scopeResource resource |> map action) true (Array.ofSeq values) (fun _ () -> ()))
                environment
                cancellationToken)

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
    /// interruption and defects remain interruption and defects. Use <c>awaitExit</c> to inspect the outcome instead.
    /// </remarks>
    /// <param name="fiber">The fiber to join.</param>
    /// <returns>A flow that completes with the fiber's value.</returns>
    /// <example>
    /// <code>
    /// let loadProfile : Flow&lt;unit, string, string&gt; = Flow.ok "profile"
    /// let loadOrders : Flow&lt;unit, string, int list&gt; = Flow.ok [ 1; 2 ]
    ///
    /// let page =
    ///     flow {
    ///         let! fiber = Flow.fork loadProfile
    ///         let! orders = loadOrders
    ///         let! profile = Fiber.join fiber
    ///         return profile, orders
    ///     }
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
    ///     match! Fiber.awaitExit fiber with
    ///     | Exit.Success value -> return Some value
    ///     | Exit.Failure _ -> return None
    /// }
    /// </code>
    /// </example>
    let awaitExit (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
        Flow(fun _ _ ->
            fiber.MarkObserved()
            Platform.awaitExitTaskAsSuccess fiber.ExitTask)

    /// Waits without consuming the outcome so an internal supervisor can decide whether to report its defect.
    let internal awaitUnobserved (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
        Flow(fun _ _ -> Platform.awaitExitTaskAsSuccess fiber.ExitTask)

    /// Marks an outcome consumed by internal supervision after an unobserved wait wins its race.
    let internal markObserved (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ ->
            fiber.MarkObserved()
            Execution.ofValue ())

    /// Reports a settled defect exactly once when no consumer observed the fiber's outcome.
    let internal reportUnobserved (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ ->
            fiber.ReportUnobserved()
            Execution.ofValue ())

    /// <summary>Returns the fiber's exit if it has settled, without waiting.</summary>
    /// <remarks>A settled fiber read this way is not marked observed; use <c>awaitExit</c> to consume its outcome.</remarks>
    /// <param name="fiber">The fiber to check.</param>
    /// <returns>A flow that succeeds with <c>Some exit</c> once the fiber has settled, otherwise <c>None</c>.</returns>
    let poll (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error> option> =
        Flow(fun _ _ -> Execution.ofValue fiber.Settled)

    /// <summary>Signals a fiber to stop without waiting for it to settle.</summary>
    /// <remarks>
    /// Cancellation is requested asynchronously, so a blocking cancellation callback cannot hold the caller.
    /// Exceptions raised by cancellation callbacks are contained and reported to the ambient fiber observer as
    /// unobserved defects associated with the target fiber. This operation does not consume the fiber's outcome;
    /// use <c>awaitExit</c>, <c>join</c>, or <c>interrupt</c> when the outcome matters.
    /// </remarks>
    /// <param name="fiber">The fiber to ask to stop.</param>
    let requestInterrupt (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ ->
            let observer = RuntimeState.current().Observer

            let report (error: exn) =
#if !FABLE_COMPILER
                match error with
                | :? AggregateException as aggregate ->
                    for inner in aggregate.Flatten().InnerExceptions do
                        FiberObserver.notifyUnobservedDefect observer (Some fiber.Metadata) inner
                | _ -> FiberObserver.notifyUnobservedDefect observer (Some fiber.Metadata) error
#else
                FiberObserver.notifyUnobservedDefect observer (Some fiber.Metadata) error
#endif

#if !FABLE_COMPILER
            // Cancellation callbacks are arbitrary user code; queue the request so this Flow always returns promptly.
            ThreadPool.QueueUserWorkItem(fun _ ->
                try
                    fiber.InterruptSource.Cancel()
                with error ->
                    report error)
            |> ignore
#else
            try
                fiber.InterruptSource.Cancel()
            with error ->
                report error
#endif
            Execution.ofValue ())

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

/// <summary>Creates and clears <see cref="T:Axial.FiberSlot`2" /> and <see cref="T:Axial.KeyedFiberSlot`3" /> values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FiberSlot =
    /// <summary>Creates an empty slot for <c>Flow.forkReplacing</c>.</summary>
    let make<'env, 'none, 'error, 'value> () : Flow<'env, 'none, FiberSlot<'error, 'value>> =
        Flow(fun _ _ -> Execution.ofValue (FiberSlot()))

    /// <summary>Creates an empty keyed slot for <c>Flow.forkReplacingKey</c>.</summary>
    let makeKeyed<'env, 'none, 'key, 'error, 'value when 'key: equality> () : Flow<'env, 'none, KeyedFiberSlot<'key, 'error, 'value>> =
        Flow(fun _ _ -> Execution.ofValue (KeyedFiberSlot()))

    // Slots discard the exit, so a defect the fiber settled with is reported instead of consumed.
    let private interruptReporting (fiber: Fiber<'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ ->
            fiber.InterruptSource.Cancel()

            Platform.awaitExitTaskAsSuccess fiber.ExitTask
            |> Execution.map (fun _ -> fiber.ReportUnobserved()))

    /// <summary>Interrupts the slot's fiber, if any, and waits for its cleanup.</summary>
    /// <remarks>A defect the fiber settles with is reported through the fiber observer exactly once.</remarks>
    /// <param name="slot">The slot to clear.</param>
    let interrupt (slot: FiberSlot<'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun environment cancellationToken ->
            match slot.Take() with
            | Some fiber -> Flow.invoke (interruptReporting fiber) environment cancellationToken
            | None -> Execution.ofValue ())

    /// <summary>Interrupts every fiber in a keyed slot and waits for their cleanup.</summary>
    /// <remarks>A defect any fiber settles with is reported through the fiber observer exactly once.</remarks>
    /// <param name="slots">The keyed slot to clear.</param>
    let interruptAll (slots: KeyedFiberSlot<'key, 'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun environment cancellationToken ->
            Flow.invoke (slots.TakeAll() |> List.map interruptReporting |> Flow.sequence) environment cancellationToken
            |> Execution.map (fun _ -> ()))

    /// <summary>Returns the number of keys whose fiber is still running.</summary>
    /// <param name="slots">The keyed slot.</param>
    let count (slots: KeyedFiberSlot<'key, 'error, 'value>) : Flow<'env, 'none, int> =
        Flow(fun _ _ -> Execution.ofValue slots.Count)
