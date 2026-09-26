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

type internal WaiterState =
    | Waiting
    | Completed
    | Cancelled
    | ShutDown

/// A suspended fiber waiting to be handed something. Its state changes only under the owning structure's gate, so
/// an interrupted waiter and a producer completing it cannot both win.
type internal Waiter<'a>(value: 'a) =
    member val Signal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    member val Value = value with get, set
    member val State = Waiting with get, set

/// What an interrupted waiter does with a handover that completed in the same instant as its interruption.
type internal RacedHandover<'a> =
    /// The handover stands, and the waiter reports it as a success. An accepted offer has already happened.
    | KeepHandover
    /// The waiter gives the handed value back under the gate, then reports the interruption. A value or permit
    /// handed to a fiber that is being interrupted must reach someone else instead of being lost.
    | ReturnHandover of ('a -> ResizeArray<Platform.Signal<unit>> -> unit)

/// A FIFO of suspended waiters guarded by its owner's gate. Members that take <c>wake</c> must be called with the
/// gate held; they collect the signals to resolve, and the caller resolves them with <c>WaitList.wakeAll</c> after
/// releasing the gate, so no waiter's continuation runs under the lock.
type internal WaitList<'a>(gate: obj) =
    let waiters = Deque<Waiter<'a>>()

    member _.Gate = gate
    member _.Count = waiters.Count

    /// Adds a waiter carrying <paramref name="value" /> (an offered value, or a placeholder for a taker).
    member _.Enqueue(value: 'a) : Waiter<'a> =
        let waiter = Waiter value
        waiters.PushBack waiter
        waiter

    /// Completes the oldest waiter by handing it <paramref name="value" />.
    member _.CompleteOldest(value: 'a, wake: ResizeArray<Platform.Signal<unit>>) =
        let waiter = waiters.PopFront()
        waiter.Value <- value
        waiter.State <- Completed
        wake.Add waiter.Signal

    /// Completes the oldest waiter by accepting the value it carries, and returns that value.
    member _.AcceptOldest(wake: ResizeArray<Platform.Signal<unit>>) : 'a =
        let waiter = waiters.PopFront()
        let value = waiter.Value
        waiter.Value <- Unchecked.defaultof<'a>
        waiter.State <- Completed
        wake.Add waiter.Signal
        value

    /// Releases every waiter as shut down.
    member _.ShutDownAll(wake: ResizeArray<Platform.Signal<unit>>) =
        for waiter in waiters.Drain() do
            waiter.Value <- Unchecked.defaultof<'a>
            waiter.State <- ShutDown
            wake.Add waiter.Signal

    member internal _.Withdraw(waiter: Waiter<'a>) = waiters.RemoveReference waiter |> ignore

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal WaitList =
    let wakeAll (wake: ResizeArray<Platform.Signal<unit>>) =
        for signal in wake do
            Platform.resolveSignal signal () |> ignore

    /// Awaits a waiter from <c>Enqueue</c>. <paramref name="settled" /> reads the outcome once the waiter is
    /// completed or shut down. On interruption, a waiter still waiting is withdrawn under the gate and
    /// <paramref name="withdrawn" /> runs, still under the gate; one completed in the same instant is resolved by
    /// <paramref name="raced" /> instead.
    let awaitWith
        (list: WaitList<'a>)
        (waiter: Waiter<'a>)
        (withdrawn: ResizeArray<Platform.Signal<unit>> -> unit)
        (raced: RacedHandover<'a>)
        (settled: Waiter<'a> -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        Execution.fold
            (fun () -> settled waiter)
            (fun cause ->
                let wake = ResizeArray()

                let keep =
                    Platform.lock list.Gate (fun () ->
                        match waiter.State with
                        | Waiting ->
                            waiter.State <- Cancelled
                            waiter.Value <- Unchecked.defaultof<'a>
                            list.Withdraw waiter
                            withdrawn wake
                            false
                        | Completed ->
                            match raced with
                            | KeepHandover -> true
                            | ReturnHandover giveBack ->
                                let value = waiter.Value
                                waiter.Value <- Unchecked.defaultof<'a>
                                waiter.State <- Cancelled
                                giveBack value wake
                                false
                        | Cancelled
                        | ShutDown -> false)

                wakeAll wake
                if keep then settled waiter else Execution.ofCause cause)
            (Platform.awaitSignal waiter.Signal cancellationToken)

    /// <c>awaitWith</c> for waiters that hold nothing else when withdrawn.
    let await list waiter raced settled cancellationToken =
        awaitWith list waiter ignore raced settled cancellationToken

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

/// A small FIFO queue of permits. Acquiring takes an available permit immediately or waits in line; releasing hands
/// the freed permit straight to the oldest waiter, if any, or returns it to the pool.
and internal PermitQueue =
    { Gate: obj
      mutable Available: int
      Waiters: WaitList<unit> }

module internal PermitQueue =
    let create (permits: int) : PermitQueue =
        let gate = obj ()

        { Gate = gate
          Available = permits
          Waiters = WaitList<unit>(gate) }

    let private releaseLocked (queue: PermitQueue) (wake: ResizeArray<Platform.Signal<unit>>) =
        if queue.Waiters.Count > 0 then
            queue.Waiters.CompleteOldest((), wake)
        else
            queue.Available <- queue.Available + 1

    /// Releases a permit: hands it directly to the oldest waiter, if any, or returns it to the pool.
    let release (queue: PermitQueue) : unit =
        let wake = ResizeArray()
        Platform.lock queue.Gate (fun () -> releaseLocked queue wake)
        WaitList.wakeAll wake

    /// Takes a permit only if one is free and nobody is waiting for it.
    let tryAcquireNow (queue: PermitQueue) : bool =
        Platform.lock queue.Gate (fun () ->
            if queue.Available > 0 then
                queue.Available <- queue.Available - 1
                true
            else
                false)

    /// Acquires a permit, waiting in FIFO order. An interrupted waiter that was granted a permit in the same
    /// instant passes it on, so interruption never loses a permit.
    let acquire (queue: PermitQueue) cancellationToken : Execution<unit, 'error> =
        let waiter =
            Platform.lock queue.Gate (fun () ->
                if queue.Available > 0 then
                    queue.Available <- queue.Available - 1
                    None
                else
                    Some(queue.Waiters.Enqueue()))

        match waiter with
        | None -> Execution.ofValue ()
        | Some waiter ->
            WaitList.await
                queue.Waiters
                waiter
                (ReturnHandover(fun () wake -> releaseLocked queue wake))
                (fun _ -> Execution.ofValue ())
                cancellationToken

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
            PermitQueue.acquire queue cancellationToken
            |> Execution.bind (fun () ->
                Execution.fold
                    (fun value ->
                        PermitQueue.release queue
                        Execution.ofValue value)
                    (fun cause ->
                        PermitQueue.release queue
                        Execution.ofCause cause)
                    (FlowInternal.invoke flow environment cancellationToken)))
