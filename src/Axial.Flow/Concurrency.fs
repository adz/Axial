namespace Axial.Flow

open System
open System.Collections.Generic

/// <summary>
/// A one-shot, typed handoff point that can be completed exactly once with a full <see cref="T:Axial.Exit`2" />.
/// </summary>
/// <remarks>
/// Use <c>Deferred</c> when fibers need to coordinate through Axial Flow outcomes rather than raw platform-native
/// primitives. Completion functions are idempotent and return <c>true</c> only to the caller that won the
/// completion race.
/// </remarks>
/// <typeparam name="error">The typed failure channel of the deferred outcome.</typeparam>
/// <typeparam name="value">The success value of the deferred outcome.</typeparam>
type Deferred<'error, 'value> =
    private
    | Deferred of Platform.Signal<Exit<'value, 'error>>

/// <summary>Flow-native helpers for one-shot typed coordination.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Deferred =
    /// <summary>Creates an empty deferred value.</summary>
    let make<'env, 'error, 'value> () : Flow<'env, 'error, Deferred<'error, 'value>> =
        Flow.ok (Deferred(Platform.newSignal ()))

    /// <summary>Waits for the deferred outcome, preserving success, typed failure, defect, or interruption.</summary>
    let await (Deferred signal: Deferred<'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            Execution.fold Execution.ofExit Execution.ofCause (Platform.awaitSignal signal cancellationToken))

    /// <summary>Attempts to complete the deferred value with a full outcome.</summary>
    let complete
        (exit: Exit<'value, 'error>)
        (Deferred signal: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        Flow.read (fun _ -> Platform.resolveSignal signal exit)

    /// <summary>Attempts to complete the deferred value successfully.</summary>
    let succeed
        (value: 'value)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        complete (Exit.Success value) deferred

    /// <summary>Attempts to complete the deferred value with a typed failure.</summary>
    let fail
        (error: 'error)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        complete (Exit.Failure(Cause.Fail error)) deferred

    /// <summary>Attempts to complete the deferred value with a defect.</summary>
    let die
        (error: exn)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        complete (Exit.Failure(Cause.Die error)) deferred

    /// <summary>Attempts to complete the deferred value as interrupted.</summary>
    let interrupt (deferred: Deferred<'error, 'value>) : Flow<'env, 'workflowError, bool> =
        complete (Exit.Failure Cause.Interrupt) deferred

/// <summary>A Flow-native semaphore handle used to limit concurrent workflow sections.</summary>
type FlowSemaphore =
    private
    | FlowSemaphore of PermitQueue

/// A small FIFO queue of permits, built on <see cref="T:Axial.Flow.Platform.Signal`1" />. Acquiring takes an
/// available permit immediately, or enqueues a waiter signal that the next <c>release</c> resolves; releasing
/// hands the freed permit straight to the oldest queued waiter, if any, or returns it to the pool otherwise.
and internal PermitQueue =
    { Gate: obj
      mutable Available: int
      Waiters: Queue<Platform.Signal<unit>> }

module internal PermitQueue =
    let create (permits: int) : PermitQueue =
        { Gate = obj ()
          Available = permits
          Waiters = Queue<Platform.Signal<unit>>() }

    /// Registers a waiter for a permit, or immediately grants one if available. Returns <c>None</c> when the
    /// permit was granted synchronously, or <c>Some signal</c> to await otherwise.
    let tryAcquire (queue: PermitQueue) : Platform.Signal<unit> option =
        Platform.lock queue.Gate (fun () ->
            if queue.Available > 0 then
                queue.Available <- queue.Available - 1
                None
            else
                let signal = Platform.newSignal ()
                queue.Waiters.Enqueue signal
                Some signal)

    /// Releases a permit: hands it directly to the oldest queued waiter, if any, or returns it to the pool.
    let release (queue: PermitQueue) : unit =
        let nextWaiter =
            Platform.lock queue.Gate (fun () ->
                if queue.Waiters.Count > 0 then
                    Some(queue.Waiters.Dequeue())
                else
                    queue.Available <- queue.Available + 1
                    None)

        match nextWaiter with
        | Some signal -> Platform.resolveSignal signal () |> ignore
        | None -> ()

/// <summary>Flow-native semaphore helpers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Semaphore =
    /// <summary>Creates a semaphore with the supplied initial permit count.</summary>
    let make (permits: int) : Flow<'env, 'error, FlowSemaphore> =
        Flow(fun _ _ ->
            if permits <= 0 then
                Execution.ofDie (ArgumentOutOfRangeException(nameof permits, "Permit count must be positive."))
            else
                Execution.ofValue (FlowSemaphore(PermitQueue.create permits)))

    /// <summary>Alias for <c>make</c>.</summary>
    let create (permits: int) : Flow<'env, 'error, FlowSemaphore> =
        make permits

    /// <summary>Runs a workflow while holding one permit and always releases the permit afterward.</summary>
    let withPermit
        (FlowSemaphore queue)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            match PermitQueue.tryAcquire queue with
            | None ->
                Execution.fold
                    (fun value ->
                        PermitQueue.release queue
                        Execution.ofValue value)
                    (fun cause ->
                        PermitQueue.release queue
                        Execution.ofCause cause)
                    (FlowInternal.invoke flow environment cancellationToken)
            | Some signal ->
                Execution.bind
                    (fun () ->
                        Execution.fold
                            (fun value ->
                                PermitQueue.release queue
                                Execution.ofValue value)
                            (fun cause ->
                                PermitQueue.release queue
                                Execution.ofCause cause)
                            (FlowInternal.invoke flow environment cancellationToken))
                    (Platform.awaitSignal signal cancellationToken))
