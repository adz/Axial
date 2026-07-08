/// The package's platform-variant surface. This file holds the FABLE_COMPILER directives for every genuinely
/// dual-bodied Flow primitive: each half declares the same names (and, where the shape itself differs between
/// platforms, the same type aliases), so the rest of the code is platform-directive-free. (Fable 5's project
/// cracker does not expose FABLE_COMPILER as an MSBuild property, so the variants live in one conditionally-halved
/// file rather than two conditionally-included ones; split them if Fable restores the property.)
///
/// Not every FABLE_COMPILER directive in the package is a true platform swap. A handful of members inside
/// otherwise dual-bodied files (BindError.fs's Task/ValueTask overloads, FlowBuilder.fs's ColdTask/Task/ValueTask
/// overloads) have no Fable-side counterpart at all. Moving those into this module would bloat Platform.fs with
/// business logic that never varies -- it would just be "the .NET body" with an empty Fable twin -- so per this
/// module's purpose (isolating genuine platform variance) they are left as `#if !FABLE_COMPILER` guarded
/// declarations in their original files.
///
/// <remarks>
/// <b>Scope of the "Fable" half below: Fable's JavaScript target specifically, not Fable in general.</b> Fable
/// also compiles to Python, Rust, Dart, PHP, and (as of this writing, newly) Erlang, each with a genuinely
/// different concurrency model -- Python has real OS threads and a GIL that does not prevent races between
/// bytecode instructions, Rust has real parallel shared-memory threads, Erlang has no shared mutable state to
/// race on in the first place (isolated processes, message passing). Every branch below guarded only by
/// <c>FABLE_COMPILER</c> -- not a more specific check -- is implicitly a "no preemption, single JS thread,
/// shared mutable state is safe to touch unsynchronized" assumption. That assumption holds for both of Fable's
/// current JS-hosted runtimes (browser and Node.js: single-threaded event loop, no <c>SharedArrayBuffer</c>-style
/// shared memory by default) but would be UNSOUND if this package ever targeted Fable.Python or Fable.Rust, and
/// moot-but-differently-shaped on Fable.Erlang (no shared state to guard, so the primitive would need a
/// message-passing shape rather than a no-op lock). These call sites are marked <c>[JS-only assumption]</c> in
/// their doc comments so a future non-JS Fable target can be grepped for and re-audited rather than silently
/// inheriting a no-op that only made sense for JS.
/// </remarks>
module internal Axial.Flow.Platform

open System
open System.Threading
open System.Threading.Tasks

// ---------------------------------------------------------------------------------------------
// Execution<'value, 'error>: the workflow's core awaitable outcome type.
// ---------------------------------------------------------------------------------------------

#if FABLE_COMPILER
/// The platform's awaitable workflow outcome. Fable has no value-type task, so plain <c>Async</c> is used.
type Execution<'value, 'error> = Async<Exit<'value, 'error>>
#else
/// The platform's awaitable workflow outcome. <c>ValueTask</c> avoids a heap allocation for the common
/// synchronously-completed case.
type Execution<'value, 'error> = ValueTask<Exit<'value, 'error>>
#endif

// ---------------------------------------------------------------------------------------------
// The shared computation expression. `async` and `task` are both ordinary F# values (builder
// instances resolved statically per compiled branch), so most of the CE *bodies* below need not be
// duplicated at all -- only the builder they run against, and the final wrap into `Execution`,
// differ.
//
// A custom builder that produces `ValueTask` directly (so even that final wrap could be dropped) was
// tried and rejected: FSharp.Core's `TaskBuilderBase`, the extension point IcedTasks/Ply use for their
// own `valueTask { }` builders, has no constructor usable from outside FSharp.Core itself on the
// FSharp.Core version this repo builds against (confirmed with a throwaway `dotnet fsi` probe --
// `inherit TaskBuilderBase()` fails with `FS1133: No constructors are available for the type
// 'TaskBuilderBase'`), so it cannot be subclassed here. `Execution<'value, 'error>` therefore stays
// `ValueTask`-shaped on .NET exactly as before; only the handful of steps *inside* a single
// primitive's body now run on a plain `Task` there, wrapped into a `ValueTask` once at the end via
// `ofAwaitable`. F#'s `task` builder already awaits `ValueTask`-typed sub-expressions natively (no
// `.AsTask()` needed), so the shared body can freely `let!`/`return!` against `Execution<_, _>` values.
//
// Where the platforms' bodies differ for a real reason -- a different concurrency primitive
// (Async.Parallel vs. Task.WhenAny), not just the CE keyword -- the function is still fully gated
// (see zipParExecution, raceExecution, startFiber, timeoutExecution, and the two async-conversion
// primitives below that need an explicit CancellationToken threaded through `Async.StartAsTask`).
// ---------------------------------------------------------------------------------------------

#if FABLE_COMPILER
/// The platform's bare (non-`Exit`-wrapped) awaitable, i.e. what `execution { }` produces.
type Awaitable<'value> = Async<'value>
/// The computation expression shared by every Platform primitive whose body is otherwise identical
/// on both platforms.
let execution = async
#else
/// The platform's bare (non-`Exit`-wrapped) awaitable, i.e. what `execution { }` produces.
type Awaitable<'value> = Task<'value>
/// The computation expression shared by every Platform primitive whose body is otherwise identical
/// on both platforms.
let execution = task
#endif

/// Wraps the result of an <c>execution { }</c> block as an <c>Execution</c>.
let ofAwaitable (awaitable: Awaitable<Exit<'value, 'error>>) : Execution<'value, 'error> =
#if FABLE_COMPILER
    awaitable
#else
    ValueTask<Exit<'value, 'error>>(awaitable)
#endif

/// Lifts an already-known exit outcome into an execution.
let ofExit (exit: Exit<'value, 'error>) : Execution<'value, 'error> =
#if FABLE_COMPILER
    async.Return exit
#else
    ValueTask<Exit<'value, 'error>>(exit)
#endif

/// Awaits an execution and folds both its success and failure channel into a follow-up execution.
let fold
    (onSuccess: 'value -> Execution<'next, 'nextError>)
    (onFailure: Cause<'error> -> Execution<'next, 'nextError>)
    (effect: Execution<'value, 'error>)
    : Execution<'next, 'nextError> =
    execution {
        let! exit = effect

        match exit with
        | Exit.Success value -> return! onSuccess value
        | Exit.Failure cause -> return! onFailure cause
    }
    |> ofAwaitable

/// Transforms both channels of an already-known exit outcome. A private mirror of <c>Exit.mapBoth</c> (defined
/// later, in Core.fs) so this file does not need to depend on modules compiled after it.
let private mapBothExit
    (onSuccess: 'value -> 'next)
    (onFailure: Cause<'error> -> Cause<'nextError>)
    (exit: Exit<'value, 'error>)
    : Exit<'next, 'nextError> =
    match exit with
    | Exit.Success value -> Exit.Success(onSuccess value)
    | Exit.Failure cause -> Exit.Failure(onFailure cause)

/// Awaits an execution and transforms both its success and failure channel with pure functions.
let mapBoth
    (onSuccess: 'value -> 'next)
    (onFailure: Cause<'error> -> Cause<'nextError>)
    (effect: Execution<'value, 'error>)
    : Execution<'next, 'nextError> =
    execution {
        let! exit = effect
        return mapBothExit onSuccess onFailure exit
    }
    |> ofAwaitable

/// Runs a thunk that produces an execution and routes both synchronous and asynchronous exceptions raised while
/// producing or awaiting it through <paramref name="onError" />, exactly as a bare <c>try/with</c> around a
/// <c>return!</c> would. This is the workhorse used by every Flow combinator that needs to observe or recover
/// from a thrown exception (cancellation handling, <c>Flow.catch</c>, resource cleanup, and similar).
let tryExecution
    (thunk: unit -> Execution<'value, 'error>)
    (onError: exn -> Execution<'value, 'error>)
    : Execution<'value, 'error> =
    execution {
        try
            return! thunk ()
        with error ->
            return! onError error
    }
    |> ofAwaitable

/// Converts a raw async operation into an execution, mapping its produced value into an exit outcome. Thrown
/// exceptions are not caught; they propagate as a faulted execution for the caller to handle (typically with
/// <c>tryExecution</c>).
/// <remarks>
/// Kept fully gated rather than sharing <c>execution { }</c>: <c>task { }</c>'s built-in support for binding a
/// bare <c>Async&lt;'a&gt;</c> observes <c>Async.DefaultCancellationToken</c>, not the token this primitive was
/// given, which would silently drop cancellation for every caller. <c>Async.StartAsTask</c> is used instead so
/// the supplied token is the one actually observed.
/// </remarks>
let executionOfAsyncUnguarded
    (cancellationToken: CancellationToken)
    (mapResult: 'value -> Exit<'v, 'e>)
    (operation: Async<'value>)
    : Execution<'v, 'e> =
#if FABLE_COMPILER
    async {
        let! value = operation
        return mapResult value
    }
#else
    ValueTask<Exit<'v, 'e>>(
        task {
            let! value = Async.StartAsTask(operation, cancellationToken = cancellationToken)
            return mapResult value
        })
#endif

/// Converts an execution into a plain <c>Async</c>, for platform-neutral callers such as <c>Flow.ToAsync</c>.
let executionToAsync (execution: Execution<'value, 'error>) : Async<Exit<'value, 'error>> =
#if FABLE_COMPILER
    execution
#else
    execution.AsTask() |> Async.AwaitTask
#endif

/// Converts a raw async operation that already produces an exit outcome into an execution.
/// <remarks>Kept fully gated for the same reason as <c>executionOfAsyncUnguarded</c>.</remarks>
let executionOfAsyncExit
    (cancellationToken: CancellationToken)
    (operation: Async<Exit<'value, 'error>>)
    : Execution<'value, 'error> =
#if FABLE_COMPILER
    operation
#else
    ValueTask<Exit<'value, 'error>>(Async.StartAsTask(operation, cancellationToken = cancellationToken))
#endif

// ---------------------------------------------------------------------------------------------
// Deed: a bare, unit-producing awaitable used for finalizers and scope cleanup.
// ---------------------------------------------------------------------------------------------

#if FABLE_COMPILER
/// A bare awaitable side effect with no result value, used for finalizers and scope cleanup.
type Deed = Async<unit>
#else
/// A bare awaitable side effect with no result value, used for finalizers and scope cleanup.
type Deed = Task
#endif

/// A finalizer registered with a <see cref="T:Axial.Flow.Scope" />.
type Finalizer = CancellationToken -> Deed

/// An already-completed deed.
let completedDeed () : Deed =
#if FABLE_COMPILER
    async.Return()
#else
    Task.CompletedTask
#endif

/// Wraps an async-disposable resource's disposal as a deed.
let disposeAsyncDeed (resource: IAsyncDisposable) : Deed =
#if FABLE_COMPILER
    ignore resource
    async.Return()
#else
    resource.DisposeAsync().AsTask()
#endif

/// Runs a scope's finalizers in reverse registration order, collecting and re-raising any failures as a single
/// exception or an <see cref="T:System.AggregateException" />.
let runFinalizers (finalizers: Finalizer[]) (cancellationToken: CancellationToken) : Deed =
    execution {
        let errors = ResizeArray<exn>()

        for index = finalizers.Length - 1 downto 0 do
            try
                do! finalizers[index] cancellationToken
            with error ->
                errors.Add error

        match errors.Count with
        | 0 -> ()
        | 1 -> raise errors[0]
        | _ -> raise (AggregateException("One or more scope finalizers failed.", errors))
    }

// ---------------------------------------------------------------------------------------------
// Mutual exclusion.
// ---------------------------------------------------------------------------------------------

/// Runs <paramref name="f" /> while holding <paramref name="gate" />. [JS-only assumption] Fable's JavaScript
/// target has no preemption within a single thread, so there is nothing to guard against there and the gate is
/// unused; .NET uses <see cref="T:System.Threading.Monitor" />. A non-JS Fable target with real shared-memory
/// threads (e.g. Fable.Rust) would need this to be a real lock, not a no-op.
let lock (gate: obj) (f: unit -> 'a) : 'a =
#if FABLE_COMPILER
    ignore gate
    f ()
#else
    Monitor.Enter gate

    try
        f ()
    finally
        Monitor.Exit gate
#endif

// ---------------------------------------------------------------------------------------------
// IServiceProvider-backed service resolution (Service.resolve).
// ---------------------------------------------------------------------------------------------

/// Resolves a service from a provider, if the platform supports dynamic resolution at all. Fable erases
/// generics at runtime, so there is no way to ask an <c>IServiceProvider</c> for <c>typeof&lt;'service&gt;</c>
/// there; it always reports no registration.
let resolveService<'service> (provider: IServiceProvider) : 'service option =
#if FABLE_COMPILER
    ignore provider
    None
#else
    match provider.GetService(typeof<'service>) with
    | null -> None
    | service -> Some(unbox<'service> service)
#endif

/// Produces the execution used when a service could not be resolved: either because the platform does not
/// support dynamic resolution at all (Fable), or because the requested service was not registered (.NET).
let serviceResolutionUnavailable<'service, 'error> () : Execution<'service, 'error> =
#if FABLE_COMPILER
    ofExit (
        Exit.Failure(
            Cause.Die(PlatformNotSupportedException("IServiceProvider service resolution is not supported on Fable."))
        )
    )
#else
    ofExit (
        Exit.Failure(
            Cause.Die(
                InvalidOperationException(
                    $"Service {typeof<'service>.Name} was not registered in the IServiceProvider."
                )
            )
        )
    )
#endif

// ---------------------------------------------------------------------------------------------
// Fiber id allocation.
// ---------------------------------------------------------------------------------------------

/// Allocates the next value from a shared counter. [JS-only assumption] Fable's JavaScript target has no
/// preemption within a single thread, so a plain increment is safe there; .NET uses an interlocked increment to
/// stay correct under concurrent fiber creation. A non-JS Fable target with real threads would need an atomic
/// increment here too.
let nextId (counter: int64 ref) : int64 =
#if FABLE_COMPILER
    counter.Value <- counter.Value + 1L
    counter.Value
#else
    Interlocked.Increment(&counter.contents)
#endif

// ---------------------------------------------------------------------------------------------
// Defect description.
// ---------------------------------------------------------------------------------------------

/// Describes a defect exception for <c>Cause.prettyPrint</c>. Fable does not expose a reliable runtime type name
/// for exceptions, so it falls back to a fixed "Exception" label.
let dieDescription (error: exn) : string =
#if FABLE_COMPILER
    $"Exception: {error.Message}"
#else
    $"{error.GetType().Name}: {error.Message}"
#endif

// ---------------------------------------------------------------------------------------------
// Ambient runtime cell (used by RuntimeState.current/withRuntime).
// ---------------------------------------------------------------------------------------------

#if FABLE_COMPILER
/// An ambient cell holding the current runtime context. [JS-only assumption] Fable's JavaScript target has no
/// preemption within a single thread, so a plain mutable field suffices. A non-JS Fable target with real threads
/// would need a thread-local (or equivalent per-strand) cell instead, and one with no shared state at all (e.g.
/// Fable.Erlang) likely would not need this primitive in this shape in the first place.
type RuntimeCell<'value> =
    { mutable Current: 'value }
#else
/// An ambient cell holding the current runtime context. .NET uses <see cref="T:System.Threading.AsyncLocal`1" />
/// so the value flows correctly across async continuations without leaking between concurrent fibers.
type RuntimeCell<'value> =
    { Current: AsyncLocal<'value> }
#endif

/// Creates a new ambient runtime cell seeded with the supplied default value.
let newCell (initial: 'value) : RuntimeCell<'value> =
#if FABLE_COMPILER
    { Current = initial }
#else
    let cell = AsyncLocal<'value>()
    cell.Value <- initial
    { Current = cell }
#endif

/// Reads the current value held by the cell, or the supplied default if none has been set (.NET's
/// <c>AsyncLocal</c> reports <c>null</c> outside any <c>withCell</c> scope).
let getCellOrDefault (fallback: unit -> 'value) (cell: RuntimeCell<'value>) : 'value =
#if FABLE_COMPILER
    cell.Current
#else
    match box cell.Current.Value with
    | null -> fallback ()
    | _ -> cell.Current.Value
#endif

/// Runs <paramref name="operation" /> with the cell holding <paramref name="value" />, restoring the previous
/// value once the operation completes (even if it throws).
let withCell (cell: RuntimeCell<'value>) (value: 'value) (operation: unit -> 'result) : 'result =
#if FABLE_COMPILER
    let previous = cell.Current
    cell.Current <- value

    try
        operation ()
    finally
        cell.Current <- previous
#else
    let previous = cell.Current.Value
    cell.Current.Value <- value

    try
        operation ()
    finally
        cell.Current.Value <- previous
#endif

// ---------------------------------------------------------------------------------------------
// Scoped execution: run a body, always close the scope, and combine the two outcomes.
// ---------------------------------------------------------------------------------------------

/// Runs <paramref name="body" />, closes <paramref name="scope" /> regardless of the outcome, and folds the
/// body's result, any exception it raised, and any exception the scope close raised through
/// <paramref name="combine" />. This is the shared execution boundary behind <c>Flow.ToExecution</c>,
/// <c>Flow.runEffect</c>, and <c>Flow.provide</c>.
let inline runScoped
    (closeScope: CancellationToken -> Deed)
    (cancellationToken: CancellationToken)
    (body: unit -> Execution<'value, 'error>)
    (combine: exn option -> exn option -> Exit<'value, 'error> option -> Exit<'value, 'error>)
    : Execution<'value, 'error> =
    execution {
        let mutable exit: Exit<'value, 'error> option = None
        let mutable executionError: exn option = None

        try
            let! result = body ()
            exit <- Some result
        with error ->
            executionError <- Some error

        let mutable cleanupError: exn option = None

        try
            do! closeScope cancellationToken
        with error ->
            cleanupError <- Some error

        return combine cleanupError executionError exit
    }
    |> ofAwaitable

// ---------------------------------------------------------------------------------------------
// Sleep and timeout.
// ---------------------------------------------------------------------------------------------

/// Suspends for the given delay, observing cancellation as an interruption.
let sleepExecution (delay: TimeSpan) (cancellationToken: CancellationToken) : Execution<unit, 'error> =
#if FABLE_COMPILER
    async {
        try
            do! Async.Sleep(int delay.TotalMilliseconds)
            return Exit.Success()
        with :? OperationCanceledException ->
            return Exit.Failure Cause.Interrupt
    }
#else
    ValueTask<Exit<unit, 'error>>(
        task {
            try
                do! Task.Delay(delay, cancellationToken)
                return Exit.Success()
            with :? OperationCanceledException ->
                return Exit.Failure Cause.Interrupt
        })
#endif

/// Runs <paramref name="operation" />, racing it against a timeout of <paramref name="after" />. Falls back to
/// <paramref name="onTimeout" /> if the timeout wins. This is the shared implementation behind
/// <c>Flow.Runtime.timeout</c>, <c>timeoutToOk</c>, <c>timeoutToError</c>, and <c>timeoutWith</c>, which differ
/// only in what they do when the timeout fires.
let timeoutExecution
    (after: TimeSpan)
    (operation: CancellationToken -> Execution<'value, 'error>)
    (cancellationToken: CancellationToken)
    (onTimeout: unit -> Execution<'value, 'error>)
    : Execution<'value, 'error> =
#if FABLE_COMPILER
    async {
        try
            let! child =
                Async.StartChild(operation cancellationToken, millisecondsTimeout = int after.TotalMilliseconds)

            return! child
        with :? TimeoutException ->
            return! onTimeout ()
    }
#else
    ValueTask<Exit<'value, 'error>>(
        task {
            let running = (operation cancellationToken).AsTask()
            let timeoutTask = Task.Delay after
            let! completed = Task.WhenAny([| running :> Task; timeoutTask |])

            if obj.ReferenceEquals(completed, timeoutTask) then
                return! onTimeout ()
            else
                return! running
        })
#endif

/// Waits for <paramref name="delay" /> and then continues with <paramref name="continuation" />. Shared by the
/// retry/repeat loops in <c>Flow.Runtime.retry</c> and <c>Schedule.retry</c>/<c>Schedule.repeat</c>.
let delayThenExecution
    (delay: TimeSpan)
    (cancellationToken: CancellationToken)
    (continuation: unit -> Execution<'value, 'error>)
    : Execution<'value, 'error> =
#if FABLE_COMPILER
    execution {
        if delay > TimeSpan.Zero then
            do! Async.Sleep(int delay.TotalMilliseconds)

        return! continuation ()
    }
    |> ofAwaitable
#else
    execution {
        if delay > TimeSpan.Zero then
            do! Task.Delay(delay, cancellationToken)

        return! continuation ()
    }
    |> ofAwaitable
#endif

// ---------------------------------------------------------------------------------------------
// Parallel composition: zipPar (fail-fast, cancels the loser) and race.
// ---------------------------------------------------------------------------------------------

/// Runs two operations concurrently, cancelling the other as soon as one of them fails, and folds their exits
/// with <paramref name="chooseParallel" /> (both settled) or <paramref name="chooseAfterCancel" /> (one failed
/// and cancelled the other).
/// <remarks>
/// The .NET branch cannot accept a shared "choose after cancel" callback the way <paramref name="chooseParallel" />
/// is shared, because that helper is used at two different generic instantiations (once with the left exit as
/// "other", once with the right exit) and F# functions are not implicitly rank-2 polymorphic. Its logic --
/// identical to <c>Flow.chooseExitAfterCancel</c> -- is inlined here instead.
/// </remarks>
let zipParExecution
    (leftOp: CancellationToken -> Execution<'left, 'error>)
    (rightOp: CancellationToken -> Execution<'right, 'error>)
    (cancellationToken: CancellationToken)
    (chooseParallel: Exit<'left, 'error> -> Exit<'right, 'error> -> Exit<'left * 'right, 'error>)
    : Execution<'left * 'right, 'error> =
#if FABLE_COMPILER
    async {
        let leftTask = async {
            let! x = leftOp cancellationToken
            return box x
        }

        let rightTask = async {
            let! x = rightOp cancellationToken
            return box x
        }

        let! results = Async.Parallel [| leftTask; rightTask |]
        let leftExit = unbox<Exit<'left, 'error>> results[0]
        let rightExit = unbox<Exit<'right, 'error>> results[1]
        return chooseParallel leftExit rightExit
    }
#else
    ValueTask<Exit<'left * 'right, 'error>>(
        task {
            let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

            let leftFiberTask = (leftOp cts.Token).AsTask()
            let rightFiberTask = (rightOp cts.Token).AsTask()

            let! completed = Task.WhenAny(leftFiberTask, rightFiberTask)

            if obj.ReferenceEquals(completed, leftFiberTask) then
                match leftFiberTask.GetAwaiter().GetResult() with
                | Exit.Failure cause ->
                    cts.Cancel()
                    let! rightExit = rightFiberTask

                    return
                        match rightExit with
                        | Exit.Failure Cause.Interrupt -> Exit.Failure cause
                        | Exit.Failure otherCause -> Exit.Failure(Cause.Both(cause, otherCause))
                        | Exit.Success _ -> Exit.Failure cause
                | Exit.Success leftValue ->
                    let! rightExit = rightFiberTask

                    return
                        match rightExit with
                        | Exit.Success rightValue -> Exit.Success(leftValue, rightValue)
                        | Exit.Failure cause -> Exit.Failure cause
            else
                match rightFiberTask.GetAwaiter().GetResult() with
                | Exit.Failure cause ->
                    cts.Cancel()
                    let! leftExit = leftFiberTask

                    return
                        match leftExit with
                        | Exit.Failure Cause.Interrupt -> Exit.Failure cause
                        | Exit.Failure leftCause -> Exit.Failure(Cause.Both(leftCause, cause))
                        | Exit.Success _ -> Exit.Failure cause
                | Exit.Success rightValue ->
                    let! leftExit = leftFiberTask

                    return
                        match leftExit with
                        | Exit.Success leftValue -> Exit.Success(leftValue, rightValue)
                        | Exit.Failure cause -> Exit.Failure cause
        })
#endif

/// Runs two operations concurrently, waits for both regardless of failure (catching any exception either one
/// raises as a defect), and folds their exits with <paramref name="chooseParallel" />. Used by
/// <c>Layer.zipPar</c>, which -- unlike <c>Flow.zipPar</c> -- has no "cancel the loser" behavior because
/// provisioning failures still need their sibling's scope to finish acquiring before either scope can close.
let zipParAllExecution
    (leftOp: unit -> Execution<'left, 'error>)
    (rightOp: unit -> Execution<'right, 'error>)
    (causeOfException: exn -> Cause<'error>)
    (chooseParallel: Exit<'left, 'error> -> Exit<'right, 'error> -> Exit<'left * 'right, 'error>)
    : Execution<'left * 'right, 'error> =
#if FABLE_COMPILER
    let runBranch (op: unit -> Execution<'a, 'error>) =
        async {
            try
                return! op ()
            with error ->
                return Exit.Failure(causeOfException error)
        }

    async {
        let! exits =
            [| async {
                   let! exit = runBranch leftOp
                   return box exit
               }
               async {
                   let! exit = runBranch rightOp
                   return box exit
               } |]
            |> Async.Parallel

        let leftExit = unbox<Exit<'left, 'error>> exits[0]
        let rightExit = unbox<Exit<'right, 'error>> exits[1]
        return chooseParallel leftExit rightExit
    }
#else
    ValueTask<Exit<'left * 'right, 'error>>(
        task {
            let runBranch (op: unit -> Execution<'a, 'error>) =
                task {
                    try
                        return! (op ()).AsTask()
                    with error ->
                        return Exit.Failure(causeOfException error)
                }

            let leftTask = runBranch leftOp
            let rightTask = runBranch rightOp

            do! Task.WhenAll(leftTask :> Task, rightTask :> Task)

            return chooseParallel leftTask.Result rightTask.Result
        })
#endif

/// Runs two operations concurrently and returns whichever settles first, cancelling the loser.
/// <remarks>Not supported on Fable: there is no cooperative way to cancel a losing branch mid-flight there.</remarks>
let raceExecution
    (leftOp: CancellationToken -> Execution<'value, 'error>)
    (rightOp: CancellationToken -> Execution<'value, 'error>)
    (cancellationToken: CancellationToken)
    : Execution<'value, 'error> =
#if FABLE_COMPILER
    ignore leftOp
    ignore rightOp
    ignore cancellationToken
    async { return failwith "Flow.race is not supported on Fable." }
#else
    ValueTask<Exit<'value, 'error>>(
        task {
            let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

            let leftFiberTask = (leftOp cts.Token).AsTask()
            let rightFiberTask = (rightOp cts.Token).AsTask()

            let! completed = Task.WhenAny(leftFiberTask, rightFiberTask)
            cts.Cancel()

            return completed.GetAwaiter().GetResult()
        })
#endif

// ---------------------------------------------------------------------------------------------
// Fibers: fork, join, interrupt.
// ---------------------------------------------------------------------------------------------

#if FABLE_COMPILER
/// The platform's handle for awaiting a forked fiber's outcome.
type ExitTask<'value, 'error> = Async<Exit<'value, 'error>>
#else
/// The platform's handle for awaiting a forked fiber's outcome.
type ExitTask<'value, 'error> = Task<Exit<'value, 'error>>
#endif

/// Starts <paramref name="run" /> as hot, detached work and returns both a cancellation source that requests its
/// interruption and a handle that completes with its final exit. <paramref name="setStatus" /> is invoked with
/// the fiber's terminal status once <paramref name="run" /> settles (including when it throws).
let startFiber
    (parentCancellationToken: CancellationToken)
    (setStatus: FiberStatus -> unit)
    (run: CancellationToken -> Execution<'value, 'error>)
    : CancellationTokenSource * ExitTask<'value, 'error> =
    // A local mirror of Cause.isInterrupted (defined later, in Core.fs) so this file does not need to
    // depend on modules compiled after it.
    let rec isInterruptedCause (cause: Cause<'error>) : bool =
        match cause with
        | Cause.Interrupt -> true
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> isInterruptedCause left || isInterruptedCause right
        | Cause.Traced(inner, _) -> isInterruptedCause inner
        | Cause.Fail _
        | Cause.Die _ -> false

    let statusFromExit (exit: Exit<'value, 'error>) =
        match exit with
        | Exit.Success _ -> FiberStatus.Succeeded
        | Exit.Failure cause when isInterruptedCause cause -> FiberStatus.Interrupted
        | Exit.Failure _ -> FiberStatus.Failed

    let causeOfException (error: exn) : Cause<'error> =
        if error :? OperationCanceledException then
            Cause.Interrupt
        else
            Cause.Die error

#if FABLE_COMPILER
    // Fable has no cooperative CancellationTokenSource linking, so a forked fiber gets an independent source.
    // Fable does not support Async.RunSynchronously, so the fiber's completion is published through a
    // hand-rolled rendezvous cell (mirroring the Signal type below) rather than Async.StartChild.
    let cts = new CancellationTokenSource()

    let mutable settled: Exit<'value, 'error> option = None
    let mutable waiters: (Exit<'value, 'error> -> unit) list = []

    let settle (exit: Exit<'value, 'error>) =
        if settled.IsNone then
            settled <- Some exit
            let toNotify = List.rev waiters
            waiters <- []

            for waiter in toNotify do
                waiter exit

    Async.StartImmediate(
        async {
            try
                let! exit = run cts.Token
                setStatus (statusFromExit exit)
                settle exit
            with error ->
                let exit = Exit.Failure(causeOfException error)
                setStatus (statusFromExit exit)
                settle exit
        })

    ignore parentCancellationToken

    let exitTask =
        async {
            match settled with
            | Some exit -> return exit
            | None -> return! Async.FromContinuations(fun (resolve, _, _) -> waiters <- resolve :: waiters)
        }

    cts, exitTask
#else
    let cts = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken)

    let exitTask =
        task {
            try
                let! exit = (run cts.Token).AsTask()
                setStatus (statusFromExit exit)
                return exit
            with error ->
                let exit = Exit.Failure(causeOfException error)
                setStatus (statusFromExit exit)
                return exit
        }

    cts, exitTask
#endif

/// Awaits a fiber's exit task as an execution.
let joinExitTask (exitTask: ExitTask<'value, 'error>) : Execution<'value, 'error> =
#if FABLE_COMPILER
    exitTask
#else
    ValueTask<Exit<'value, 'error>>(exitTask)
#endif

/// Awaits a fiber's exit task and wraps it as an always-successful execution, used by <c>Flow.interrupt</c> to
/// report the fiber's final outcome without itself being able to fail.
let awaitExitTaskAsSuccess (exitTask: ExitTask<'value, 'error>) : Execution<Exit<'value, 'error>, 'none> =
    execution {
        let! exit = exitTask
        return Exit.Success exit
    }
    |> ofAwaitable

// ---------------------------------------------------------------------------------------------
// Signal: a single-assignment async rendezvous cell shared by Concurrency.fs (Deferred, Semaphore)
// and Stm.fs (the retry/commit wait).
// ---------------------------------------------------------------------------------------------

#if FABLE_COMPILER
/// A single-assignment async rendezvous cell. [JS-only assumption] Fable's JavaScript target backs this with a
/// resolved-value option plus a list of pending waiter continuations, woken in registration order once the
/// signal resolves. A non-JS Fable target with real threads would need the resolve/register race itself
/// synchronized, not just the waiter list built portably.
type Signal<'value> =
    { mutable Value: 'value option
      mutable Waiters: ('value -> unit) list }
#else
/// A single-assignment async rendezvous cell. .NET backs this with a
/// <see cref="T:System.Threading.Tasks.TaskCompletionSource`1" />.
type Signal<'value> = TaskCompletionSource<'value>
#endif

/// Creates a new, unresolved signal.
let newSignal<'value> () : Signal<'value> =
#if FABLE_COMPILER
    { Value = None; Waiters = [] }
#else
    TaskCompletionSource<'value>(TaskCreationOptions.RunContinuationsAsynchronously)
#endif

/// Resolves the signal with a value. Idempotent: returns <c>true</c> only to the caller that won the race to
/// resolve it first, and wakes every waiter registered so far.
let resolveSignal (signal: Signal<'value>) (value: 'value) : bool =
#if FABLE_COMPILER
    match signal.Value with
    | Some _ -> false
    | None ->
        signal.Value <- Some value
        let waiters = List.rev signal.Waiters
        signal.Waiters <- []

        for waiter in waiters do
            waiter value

        true
#else
    signal.TrySetResult value
#endif

/// Awaits the signal's value as an execution, observing cancellation as an interruption. Multiple callers may
/// await the same signal concurrently; cancelling one caller's wait does not affect the others or the signal
/// itself.
let awaitSignal (signal: Signal<'value>) (cancellationToken: CancellationToken) : Execution<'value, 'error> =
#if FABLE_COMPILER
    match signal.Value with
    | Some value -> ofExit (Exit.Success value)
    | None ->
        if cancellationToken.IsCancellationRequested then
            ofExit (Exit.Failure Cause.Interrupt)
        else
            async {
                return!
                    Async.FromContinuations(fun (resolveContinuation, _, _) ->
                        let mutable settled = false

                        let settle (exit: Exit<'value, 'error>) =
                            if not settled then
                                settled <- true
                                resolveContinuation exit

                        signal.Waiters <- (fun value -> settle (Exit.Success value)) :: signal.Waiters
                        cancellationToken.Register(fun () -> settle (Exit.Failure Cause.Interrupt)) |> ignore)
            }
#else
    ValueTask<Exit<'value, 'error>>(
        task {
            if cancellationToken.IsCancellationRequested then
                return Exit.Failure Cause.Interrupt
            else
                let cancellation = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                use _registration =
                    cancellationToken.Register(fun () -> cancellation.TrySetResult() |> ignore)

                let! completed = Task.WhenAny(signal.Task :> Task, cancellation.Task :> Task)

                if obj.ReferenceEquals(completed, signal.Task :> Task) then
                    return Exit.Success signal.Task.Result
                else
                    return Exit.Failure Cause.Interrupt
        })
#endif
