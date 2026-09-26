namespace Axial

open System
open System.Collections.Generic

/// A growable ring buffer with O(1) operations at both ends. Portable to Fable, unlike LinkedList.
type internal Deque<'a>() =
    let mutable items: 'a array = Array.zeroCreate 4
    let mutable head = 0
    let mutable count = 0

    let grow () =
        let next: 'a array = Array.zeroCreate (items.Length * 2)

        for index in 0 .. count - 1 do
            next[index] <- items[(head + index) % items.Length]

        items <- next
        head <- 0

    member _.Count = count

    member _.PushBack(value: 'a) =
        if count = items.Length then grow ()
        items[(head + count) % items.Length] <- value
        count <- count + 1

    member _.PushFront(value: 'a) =
        if count = items.Length then grow ()
        head <- (head - 1 + items.Length) % items.Length
        items[head] <- value
        count <- count + 1

    member _.PopFront() : 'a =
        let value = items[head]
        items[head] <- Unchecked.defaultof<'a>
        head <- (head + 1) % items.Length
        count <- count - 1
        value

    /// Removes the first element that is reference-equal to <paramref name="value" />, preserving order.
    member _.RemoveReference(value: 'a) : bool =
        let mutable found = -1
        let mutable index = 0

        while found < 0 && index < count do
            if obj.ReferenceEquals(items[(head + index) % items.Length], value) then found <- index
            index <- index + 1

        if found < 0 then
            false
        else
            for shift in found .. count - 2 do
                items[(head + shift) % items.Length] <- items[(head + shift + 1) % items.Length]

            items[(head + count - 1) % items.Length] <- Unchecked.defaultof<'a>
            count <- count - 1
            true

    member _.Drain() : 'a list =
        let values = [ for index in 0 .. count - 1 -> items[(head + index) % items.Length] ]
        items <- Array.zeroCreate 4
        head <- 0
        count <- 0
        values

/// <summary>A validated upper bound for concurrent Flow operations.</summary>
type Parallelism = private Parallelism of int

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Parallelism =
    /// <summary>Creates a positive concurrency bound.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is not positive.</exception>
    let bounded count =
        if count <= 0 then invalidArg (nameof count) "Parallelism must be positive."
        Parallelism count

    let internal value (Parallelism count) = count

/// <summary>
/// A one-shot, typed handoff point that can be completed exactly once with a full <see cref="T:Axial.Exit`2">Exit</see>.
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
        // Allocated when the flow runs, so each run of the same flow value gets its own deferred.
        Flow(fun _ _ -> Execution.ofValue (Deferred(Platform.newSignal ())))

    /// <summary>Waits for the deferred outcome, preserving success, typed failure, defect, or interruption.</summary>
    let await (deferred: Deferred<'error, 'value>) : Flow<'env, 'error, 'value> =
        let (Deferred signal) = deferred
        Flow(fun _ cancellationToken ->
            Execution.fold Execution.ofExit Execution.ofCause (Platform.awaitSignal signal cancellationToken))

    /// <summary>Attempts to complete the deferred value with a full outcome.</summary>
    let complete
        (exit: Exit<'value, 'error>)
        (deferred: Deferred<'error, 'value>)
        : Flow<'env, 'workflowError, bool> =
        let (Deferred signal) = deferred
        Flow.envWith (fun _ -> Platform.resolveSignal signal exit)

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
    internal
    | FlowSemaphore of PermitQueue

/// A workflow waiting for a permit. <c>Granted</c> changes only under the queue's gate, so an interrupted waiter and
/// a release that hands it the permit cannot both win.
and internal PermitWaiter() =
    member val Signal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    member val Granted = false with get, set

/// A small FIFO queue of permits, built on <see cref="T:Axial.Platform.Signal`1" />. Acquiring takes an
/// available permit immediately, or enqueues a waiter that the next <c>release</c> grants; releasing
/// hands the freed permit straight to the oldest queued waiter, if any, or returns it to the pool otherwise.
and internal PermitQueue =
    { Gate: obj
      mutable Available: int
      Waiters: Deque<PermitWaiter> }

module internal PermitQueue =
    let create (permits: int) : PermitQueue =
        { Gate = obj ()
          Available = permits
          Waiters = Deque<PermitWaiter>() }

    /// Grants a permit immediately if one is available (<c>None</c>), or enqueues a waiter to await (<c>Some</c>).
    let tryAcquire (queue: PermitQueue) : PermitWaiter option =
        Platform.lock queue.Gate (fun () ->
            if queue.Available > 0 then
                queue.Available <- queue.Available - 1
                None
            else
                let waiter = PermitWaiter()
                queue.Waiters.PushBack waiter
                Some waiter)

    /// Releases a permit: hands it directly to the oldest queued waiter, if any, or returns it to the pool.
    let release (queue: PermitQueue) : unit =
        let nextWaiter =
            Platform.lock queue.Gate (fun () ->
                if queue.Waiters.Count > 0 then
                    let waiter = queue.Waiters.PopFront()
                    waiter.Granted <- true
                    Some waiter
                else
                    queue.Available <- queue.Available + 1
                    None)

        match nextWaiter with
        | Some waiter -> Platform.resolveSignal waiter.Signal () |> ignore
        | None -> ()

    /// Withdraws an interrupted waiter. If it was granted a permit in the same instant, the permit is released
    /// again rather than lost.
    let withdraw (queue: PermitQueue) (waiter: PermitWaiter) : unit =
        let wasGranted =
            Platform.lock queue.Gate (fun () ->
                if waiter.Granted then
                    true
                else
                    queue.Waiters.RemoveReference waiter |> ignore
                    false)

        if wasGranted then release queue

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

    /// <summary>Runs a workflow while holding one permit and always releases the permit afterward.</summary>
    let withPermit
        (semaphore: FlowSemaphore)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        let (FlowSemaphore queue) = semaphore
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
            | Some waiter ->
                Execution.fold
                    (fun () ->
                        Execution.fold
                            (fun value ->
                                PermitQueue.release queue
                                Execution.ofValue value)
                            (fun cause ->
                                PermitQueue.release queue
                                Execution.ofCause cause)
                            (FlowInternal.invoke flow environment cancellationToken))
                    (fun cause ->
                        PermitQueue.withdraw queue waiter
                        Execution.ofCause cause)
                    (Platform.awaitSignal waiter.Signal cancellationToken))
