namespace Axial

open System

/// <summary>What a queue, or a hub subscription, does with a value offered while it is full.</summary>
/// <remarks>
/// <para>
/// Lossless delivery needs either back-pressure or unbounded memory. <c>BackPressure</c> keeps every value by making the
/// producer wait; <c>Unbounded</c> keeps every value by growing without limit; <c>Dropping</c> and <c>Sliding</c> never
/// make the producer wait and lose values instead.
/// </para>
/// <para>A non-positive capacity fails the flow that uses the strategy with a defect.</para>
/// </remarks>
[<RequireQualifiedAccess>]
type QueueStrategy =
    /// <summary>Lossless. A full buffer suspends the producer until a value is taken.</summary>
    | BackPressure of capacity: int
    /// <summary>A full buffer discards the new value.</summary>
    | Dropping of capacity: int
    /// <summary>A full buffer evicts its oldest value to make room for the new one.</summary>
    | Sliding of capacity: int
    /// <summary>Lossless and never suspends the producer; memory grows with the backlog.</summary>
    | Unbounded

/// <summary>
/// The consuming side of a FIFO queue: take values, inspect the backlog, and shut the queue down.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="T:Axial.Queue`1" /> is a <c>Dequeue</c>, so the <c>Dequeue</c> functions accept a queue directly.
/// <c>Hub.subscribe</c> returns a bare <c>Dequeue</c>, so a subscriber can take from its subscription but cannot offer
/// into it.
/// </para>
/// <para>
/// Shutdown ends a queue without discarding its backlog. Fibers suspended in a take or an offer complete as
/// interruptions, later offers are interrupted, and later takes return the remaining values before they are
/// interrupted. A consumer can therefore finish the backlog after shutdown, which lets a writer flush when an
/// application stops.
/// </para>
/// <para>
/// Interrupting a suspended take never loses a value, and interrupting a suspended offer never enqueues its value.
/// Suspended takers and suspended offerers are each served in FIFO order.
/// </para>
/// </remarks>
/// <typeparam name="a">The type of the queued values.</typeparam>
type Dequeue<'a> internal (strategy: QueueStrategy) =
    let gate = obj ()

    member internal _.Gate = gate
    member internal _.Strategy = strategy
    member val internal Buffer = Deque<'a>()
    member val internal Takers = WaitList<'a>(gate)
    member val internal Offerers = WaitList<'a>(gate)
    member val internal ShutdownSignal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    member val internal IsShut = false with get, set
    /// Runs once, after the queue shuts down; a hub uses it to forget a subscription its consumer ended.
    member val internal OnShutdown: unit -> unit = ignore with get, set

    member internal _.Capacity =
        match strategy with
        | QueueStrategy.BackPressure capacity
        | QueueStrategy.Dropping capacity
        | QueueStrategy.Sliding capacity -> Some capacity
        | QueueStrategy.Unbounded -> None

    /// <summary>Describes the queue without reading its contents.</summary>
    override this.ToString() =
        match this.Capacity with
        | Some limit -> $"Dequeue(capacity {limit})"
        | None -> "Dequeue(unbounded)"

/// <summary>
/// An asynchronous FIFO queue for handing values between fibers, with an overflow strategy chosen at creation.
/// </summary>
/// <remarks>
/// Create one with <c>Queue.make</c> or a shorthand such as <c>Queue.bounded</c>, offer with <c>Queue.offer</c>, and
/// take with the <c>Dequeue</c> functions. A queue is meant for one logical consumer; use <c>Hub</c> when zero or many
/// consumers each need every value.
/// </remarks>
/// <typeparam name="a">The type of the queued values.</typeparam>
[<Sealed>]
type Queue<'a> internal (strategy: QueueStrategy) =
    inherit Dequeue<'a>(strategy)

    /// <summary>Describes the queue without reading its contents.</summary>
    override this.ToString() =
        match this.Capacity with
        | Some limit -> $"Queue(capacity {limit})"
        | None -> "Queue(unbounded)"

type internal OfferOutcome<'a> =
    | Accepted
    /// Accepted by a sliding queue that evicted its oldest value to make room.
    | AcceptedEvicting
    /// Discarded by a full dropping queue.
    | Discarded
    | OfferSuspended of Waiter<'a>
    | OfferRejected

type internal TakeOutcome<'a> =
    | Taken of 'a
    | TakeSuspended of Waiter<'a>
    | Exhausted

/// Queue operations over raw executions. Functions named <c>...Locked</c> require the caller to hold the gate and
/// add the signals they complete to <c>wake</c>; callers resolve those after releasing the gate.
module internal QueueCore =
    let validate (strategy: QueueStrategy) : exn option =
        match strategy with
        | QueueStrategy.BackPressure capacity
        | QueueStrategy.Dropping capacity
        | QueueStrategy.Sliding capacity when capacity <= 0 ->
            Some(ArgumentOutOfRangeException(nameof capacity, "Queue capacity must be positive."))
        | _ -> None

    let wakeAll wake = WaitList.wakeAll wake

    let private hasRoom (queue: Dequeue<'a>) =
        match queue.Capacity with
        | Some limit -> queue.Buffer.Count < limit
        | None -> true

    /// Hands a value to the oldest waiting taker, or buffers it. Takers wait only while the buffer is empty.
    let private deliverLocked (queue: Dequeue<'a>) (value: 'a) (wake: ResizeArray<Platform.Signal<unit>>) =
        if queue.Takers.Count > 0 then
            queue.Takers.CompleteOldest(value, wake)
        else
            queue.Buffer.PushBack value

    /// Moves suspended offerers into freed buffer space in FIFO order.
    let private refillLocked (queue: Dequeue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) =
        while queue.Offerers.Count > 0 && hasRoom queue do
            queue.Buffer.PushBack(queue.Offerers.AcceptOldest wake)

    /// Returns values that a consumer took but could not keep because it was interrupted. They are older than
    /// anything still buffered, so they go to waiting takers first and otherwise back to the front, in order.
    let private giveBackLocked (queue: Dequeue<'a>) (values: 'a list) (wake: ResizeArray<Platform.Signal<unit>>) =
        let rest = ResizeArray<'a>()

        for value in values do
            if rest.Count = 0 && queue.Takers.Count > 0 then
                queue.Takers.CompleteOldest(value, wake)
            else
                rest.Add value

        for index in rest.Count - 1 .. -1 .. 0 do
            match queue.Strategy with
            // A full sliding queue would evict its oldest value next anyway, and the returned value is the oldest.
            | QueueStrategy.Sliding capacity when queue.Buffer.Count >= capacity -> ()
            // Lossless strategies may briefly exceed capacity rather than drop a value that was accepted.
            | _ -> queue.Buffer.PushFront rest[index]

    let offerLocked (queue: Dequeue<'a>) (value: 'a) (wake: ResizeArray<Platform.Signal<unit>>) : OfferOutcome<'a> =
        if queue.IsShut then
            OfferRejected
        elif queue.Takers.Count > 0 || hasRoom queue then
            deliverLocked queue value wake
            Accepted
        else
            match queue.Strategy with
            | QueueStrategy.BackPressure _
            | QueueStrategy.Unbounded -> OfferSuspended(queue.Offerers.Enqueue value)
            | QueueStrategy.Dropping _ -> Discarded
            | QueueStrategy.Sliding _ ->
                queue.Buffer.PopFront() |> ignore
                queue.Buffer.PushBack value
                AcceptedEvicting

    let takeLocked (queue: Dequeue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) : TakeOutcome<'a> =
        if queue.Buffer.Count > 0 then
            let value = queue.Buffer.PopFront()
            refillLocked queue wake
            Taken value
        elif queue.IsShut then
            Exhausted
        else
            TakeSuspended(queue.Takers.Enqueue Unchecked.defaultof<'a>)

    let takeUpToLocked (queue: Dequeue<'a>) (max: int) (wake: ResizeArray<Platform.Signal<unit>>) : 'a list =
        let taken = ResizeArray<'a>()

        while taken.Count < max && queue.Buffer.Count > 0 do
            taken.Add(queue.Buffer.PopFront())

        refillLocked queue wake
        List.ofSeq taken

    let shutdown (queue: Dequeue<'a>) =
        let wake = ResizeArray()

        let changed =
            Platform.lock queue.Gate (fun () ->
                if queue.IsShut then
                    false
                else
                    queue.IsShut <- true
                    queue.Takers.ShutDownAll wake
                    queue.Offerers.ShutDownAll wake
                    wake.Add queue.ShutdownSignal
                    true)

        wakeAll wake

        if changed then
            queue.OnShutdown()

    /// Waits for a suspended offer. An offer interrupted before a taker accepted its value is withdrawn and never
    /// enqueued; one accepted in the same instant as the interruption has already happened and reports success.
    let awaitOffer (queue: Dequeue<'a>) (offerer: Waiter<'a>) cancellationToken : Execution<unit, 'error> =
        WaitList.await
            queue.Offerers
            offerer
            KeepHandover
            (fun offerer ->
                match offerer.State with
                | Completed -> Execution.ofValue ()
                | _ -> Execution.ofCause Cause.Interrupt)
            cancellationToken

    /// Waits for a suspended take. A taker interrupted after a value was handed to it gives back that value, and any
    /// values it collected earlier, so interruption never loses an element.
    let private awaitTakeHolding
        (queue: Dequeue<'a>)
        (taker: Waiter<'a>)
        (held: 'a list)
        (onShutdown: unit -> Execution<'result, 'error>)
        (onValue: 'a -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        WaitList.awaitWith
            queue.Takers
            taker
            (fun wake -> giveBackLocked queue held wake)
            (ReturnHandover(fun value wake -> giveBackLocked queue (held @ [ value ]) wake))
            (fun taker ->
                match taker.State with
                | Completed ->
                    let value = taker.Value
                    taker.Value <- Unchecked.defaultof<'a>
                    onValue value
                | _ -> onShutdown ())
            cancellationToken

    /// Takes one value, suspending while the queue is empty. <paramref name="onShutdown" /> runs once the queue
    /// is shut down and drained.
    let take
        (queue: Dequeue<'a>)
        (onShutdown: unit -> Execution<'result, 'error>)
        (onValue: 'a -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        let wake = ResizeArray()
        let outcome = Platform.lock queue.Gate (fun () -> takeLocked queue wake)
        wakeAll wake

        match outcome with
        | Taken value -> onValue value
        | Exhausted -> onShutdown ()
        | TakeSuspended taker -> awaitTakeHolding queue taker [] onShutdown onValue cancellationToken

    /// Takes between <paramref name="min" /> and <paramref name="max" /> values, suspending until <c>min</c> are
    /// available. After shutdown it returns whatever remains, if anything. An interruption gives every collected
    /// value back in order.
    let takeBetween (queue: Dequeue<'a>) (min: int) (max: int) cancellationToken : Execution<'a list, 'error> =
        Execution.loop [] (fun (held: 'a list) ->
            let wake = ResizeArray()

            let outcome =
                Platform.lock queue.Gate (fun () ->
                    let collected = held @ takeUpToLocked queue (max - held.Length) wake

                    if collected.Length >= min || (queue.IsShut && not collected.IsEmpty) then
                        Choice1Of3 collected
                    elif queue.IsShut then
                        Choice2Of3()
                    else
                        Choice3Of3(collected, queue.Takers.Enqueue Unchecked.defaultof<'a>))

            wakeAll wake

            match outcome with
            | Choice1Of3 collected -> Execution.ofValue (Platform.Break collected)
            | Choice2Of3() -> Execution.ofCause Cause.Interrupt
            | Choice3Of3(collected, taker) ->
                awaitTakeHolding
                    queue
                    taker
                    collected
                    (fun () ->
                        if collected.IsEmpty then
                            Execution.ofCause Cause.Interrupt
                        else
                            Execution.ofValue (Platform.Break collected))
                    (fun value -> Execution.ofValue (Platform.Continue(collected @ [ value ])))
                    cancellationToken)

    let offer (queue: Dequeue<'a>) (value: 'a) cancellationToken : Execution<OfferOutcome<'a>, 'error> =
        let wake = ResizeArray()
        let outcome = Platform.lock queue.Gate (fun () -> offerLocked queue value wake)
        wakeAll wake

        match outcome with
        | OfferRejected -> Execution.ofCause Cause.Interrupt
        | OfferSuspended offerer -> awaitOffer queue offerer cancellationToken |> Execution.map (fun () -> Accepted)
        | outcome -> Execution.ofValue outcome

/// <summary>Takes values from a <see cref="T:Axial.Dequeue`1" />, inspects it, and shuts it down.</summary>
/// <remarks>Every function here also accepts a <see cref="T:Axial.Queue`1" />.</remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Dequeue =
    /// <summary>Removes the oldest value, suspending until one is available.</summary>
    /// <remarks>After shutdown this returns the remaining values, then is interrupted.</remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (jobs: Queue&lt;string&gt;) = Queue.bounded 8
    ///     do! jobs |&gt; Queue.offer "job" |&gt; Flow.ignore
    ///     return! Dequeue.take jobs
    /// }
    /// </code>
    /// </example>
    let take (queue: Dequeue<'a>) : Flow<'env, 'error, 'a> =
        Flow(fun _ cancellationToken ->
            QueueCore.take queue (fun () -> Execution.ofCause Cause.Interrupt) Execution.ofValue cancellationToken)

    /// <summary>Removes the oldest value if one is available, without suspending.</summary>
    let poll (queue: Dequeue<'a>) : Flow<'env, 'error, 'a option> =
        Flow(fun _ _ ->
            let wake = ResizeArray()
            let values = Platform.lock queue.Gate (fun () -> QueueCore.takeUpToLocked queue 1 wake)
            QueueCore.wakeAll wake
            Execution.ofValue (List.tryHead values))

    /// <summary>Removes up to <paramref name="max" /> available values in FIFO order, without suspending.</summary>
    /// <remarks>Returns an empty list when nothing is available. A negative maximum fails with a defect.</remarks>
    let takeUpTo (max: int) (queue: Dequeue<'a>) : Flow<'env, 'error, 'a list> =
        Flow(fun _ _ ->
            if max < 0 then
                Execution.ofDie (ArgumentOutOfRangeException(nameof max, "Maximum cannot be negative."))
            else
                let wake = ResizeArray()
                let values = Platform.lock queue.Gate (fun () -> QueueCore.takeUpToLocked queue max wake)
                QueueCore.wakeAll wake
                Execution.ofValue values)

    /// <summary>
    /// Removes between <paramref name="min" /> and <paramref name="max" /> values in FIFO order, suspending until at
    /// least <paramref name="min" /> are available.
    /// </summary>
    /// <remarks>
    /// This is the batching consumer's take: it waits for work, then takes everything that has accumulated up to a
    /// batch limit. After shutdown it returns the remaining values even if there are fewer than <c>min</c>, and is
    /// interrupted once nothing remains. If it is interrupted while waiting, every value it had collected goes back to
    /// the front of the queue in order. <c>min</c> must be at least 1 and no greater than <c>max</c>; otherwise the
    /// flow fails with a defect.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Wait for at least one sample, then write up to 500 in one batch.
    /// samples |&gt; Dequeue.takeBetween 1 500
    /// </code>
    /// </example>
    let takeBetween (min: int) (max: int) (queue: Dequeue<'a>) : Flow<'env, 'error, 'a list> =
        Flow(fun _ cancellationToken ->
            if min < 1 || max < min then
                Execution.ofDie (
                    ArgumentOutOfRangeException(nameof min, "takeBetween needs 1 <= min <= max.")
                )
            else
                QueueCore.takeBetween queue min max cancellationToken)

    /// <summary>Removes every available value in FIFO order, without suspending.</summary>
    let takeAll (queue: Dequeue<'a>) : Flow<'env, 'error, 'a list> =
        Flow(fun _ _ ->
            let wake = ResizeArray()
            let values = Platform.lock queue.Gate (fun () -> QueueCore.takeUpToLocked queue Int32.MaxValue wake)
            QueueCore.wakeAll wake
            Execution.ofValue values)

    /// <summary>Returns the number of buffered values.</summary>
    /// <remarks>Use it on a hub subscription to raise an alarm before a lossless subscriber's buffer fills.</remarks>
    let size (queue: Dequeue<'a>) : Flow<'env, 'error, int> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock queue.Gate (fun () -> queue.Buffer.Count)))

    /// <summary>Returns the queue's capacity, or <c>None</c> for an unbounded queue.</summary>
    let capacity (queue: Dequeue<'a>) : int option = queue.Capacity

    /// <summary>Shuts the queue down. Calling it again has no effect.</summary>
    /// <remarks>
    /// Either side may shut a queue down. On a hub subscription this unsubscribes: the hub stops delivering to it,
    /// and a publisher waiting on it moves on.
    /// </remarks>
    let shutdown (queue: Dequeue<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            QueueCore.shutdown queue
            Execution.ofValue ())

    /// <summary>Returns whether the queue has been shut down.</summary>
    let isShutdown (queue: Dequeue<'a>) : Flow<'env, 'error, bool> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock queue.Gate (fun () -> queue.IsShut)))

    /// <summary>Suspends until the queue is shut down.</summary>
    let awaitShutdown (queue: Dequeue<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken -> Platform.awaitSignal queue.ShutdownSignal cancellationToken)

/// <summary>Creates queues and offers values to them.</summary>
/// <remarks>Take values, inspect, and shut a queue down with the <c>Dequeue</c> functions.</remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Queue =
    /// <summary>Creates a queue with the given overflow strategy.</summary>
    /// <remarks>A non-positive capacity fails the returned flow with a defect.</remarks>
    /// <example><code>Queue.make (QueueStrategy.Sliding 1)</code></example>
    let make (strategy: QueueStrategy) : Flow<'env, 'error, Queue<'a>> =
        Flow(fun _ _ ->
            match QueueCore.validate strategy with
            | Some error -> Execution.ofDie error
            | None -> Execution.ofValue (Queue<'a>(strategy)))

    /// <summary>Creates a queue that is shut down when the current scope closes.</summary>
    /// <remarks>
    /// Use it inside <c>Flow.scoped</c>, a forked fiber, or an application root. Closing the scope interrupts fibers
    /// suspended on the queue and ends any <c>FlowStream.fromDequeue</c> consumer after it drains the backlog.
    /// </remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (jobs: Queue&lt;string&gt;) = Queue.makeScoped (QueueStrategy.BackPressure 64)
    ///     do! jobs |&gt; Queue.offer "first job" |&gt; Flow.ignore
    /// }
    /// |&gt; Flow.scoped
    /// </code>
    /// </example>
    let makeScoped (strategy: QueueStrategy) : Flow<'env, 'error, Queue<'a>> =
        Flow(fun _ _ ->
            match QueueCore.validate strategy with
            | Some error -> Execution.ofDie error
            | None ->
                let queue = Queue<'a>(strategy)
                let scope = RuntimeState.current().Scope

                let key =
                    scope.Register(fun _ ->
                        QueueCore.shutdown queue
                        Platform.completedDeed ())

                // A queue shut down before its scope closes drops the registration, so a long-lived scope does not
                // retain it.
                queue.OnShutdown <- fun () -> scope.Unregister key

                Execution.ofValue queue)

    /// <summary>Creates a lossless queue whose <c>offer</c> suspends while it holds <paramref name="capacity" /> values.</summary>
    /// <remarks>Shorthand for <c>Queue.make (QueueStrategy.BackPressure capacity)</c>.</remarks>
    /// <example><code>Queue.bounded 64</code></example>
    let bounded (capacity: int) : Flow<'env, 'error, Queue<'a>> = make (QueueStrategy.BackPressure capacity)

    /// <summary>Creates a queue whose <c>offer</c> discards the new value and returns <c>false</c> when full.</summary>
    /// <remarks>Shorthand for <c>Queue.make (QueueStrategy.Dropping capacity)</c>.</remarks>
    let dropping (capacity: int) : Flow<'env, 'error, Queue<'a>> = make (QueueStrategy.Dropping capacity)

    /// <summary>Creates a queue whose <c>offer</c> evicts the oldest value when full and returns <c>true</c>.</summary>
    /// <remarks>Shorthand for <c>Queue.make (QueueStrategy.Sliding capacity)</c>.</remarks>
    let sliding (capacity: int) : Flow<'env, 'error, Queue<'a>> = make (QueueStrategy.Sliding capacity)

    /// <summary>Creates a queue that never rejects or suspends an <c>offer</c>.</summary>
    /// <remarks>Memory grows with the backlog; prefer a bounded queue unless producers are otherwise limited.</remarks>
    let unbounded () : Flow<'env, 'error, Queue<'a>> = make QueueStrategy.Unbounded

    /// <summary>Adds a value according to the queue's strategy.</summary>
    /// <returns>
    /// <c>true</c> when the value was accepted, or <c>false</c> when a dropping queue discarded it. A bounded queue
    /// suspends until space exists. The flow is interrupted if the queue is or becomes shut down first.
    /// </returns>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (jobs: Queue&lt;string&gt;) = Queue.bounded 8
    ///     return! jobs |&gt; Queue.offer "job"
    /// }
    /// </code>
    /// </example>
    let offer (value: 'a) (queue: Queue<'a>) : Flow<'env, 'error, bool> =
        Flow(fun _ cancellationToken ->
            QueueCore.offer queue value cancellationToken
            |> Execution.map (fun outcome -> outcome <> Discarded))

    /// <summary>Adds values in order according to the queue's strategy.</summary>
    /// <returns>
    /// <c>true</c> when every value was accepted. A dropping queue accepts values while it has room and returns
    /// <c>false</c> if it discarded any. A bounded queue suspends as needed; values from other producers may be
    /// interleaved between the batch's values, but the batch's own order is kept. If the flow is interrupted part
    /// way, the values already accepted stay in the queue.
    /// </returns>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (samples: Queue&lt;float&gt;) = Queue.unbounded ()
    ///     do! samples |&gt; Queue.offerAll [ 1.0; 2.0; 3.0 ] |&gt; Flow.ignore
    ///     return! samples |&gt; Dequeue.takeUpTo 500
    /// }
    /// </code>
    /// </example>
    let offerAll (values: 'a seq) (queue: Queue<'a>) : Flow<'env, 'error, bool> =
        Flow(fun _ cancellationToken ->
            let items = Seq.toArray values

            Execution.loop (0, true) (fun (start, allAccepted) ->
                let wake = ResizeArray()
                let index = ref start
                let accepted = ref allAccepted
                let suspended = ref None
                let rejected = ref false

                Platform.lock queue.Gate (fun () ->
                    while index.Value < items.Length && suspended.Value.IsNone && not rejected.Value do
                        match QueueCore.offerLocked queue items[index.Value] wake with
                        | Accepted
                        | AcceptedEvicting -> index.Value <- index.Value + 1
                        | Discarded ->
                            accepted.Value <- false
                            index.Value <- index.Value + 1
                        | OfferSuspended offerer ->
                            suspended.Value <- Some offerer
                            index.Value <- index.Value + 1
                        | OfferRejected -> rejected.Value <- true)

                QueueCore.wakeAll wake

                if rejected.Value then
                    Execution.ofCause Cause.Interrupt
                else
                    match suspended.Value with
                    | None -> Execution.ofValue (Platform.Break accepted.Value)
                    | Some offerer ->
                        QueueCore.awaitOffer queue offerer cancellationToken
                        |> Execution.map (fun () -> Platform.Continue(index.Value, accepted.Value))))
