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

/// <summary>A snapshot of a queue's backlog and of what it has done with offered values since it was created.</summary>
/// <remarks>
/// The counters only grow, so a monitor can report rates from the difference between two snapshots. Read them with
/// <c>Dequeue.stats</c>, for example to raise an alarm before a lossless hub subscriber fills its buffer or to count a
/// display's lost updates.
/// </remarks>
type QueueStats =
    {
        /// <summary>Values currently buffered.</summary>
        Size: int
        /// <summary>The capacity, or <c>None</c> for an unbounded queue.</summary>
        Capacity: int option
        /// <summary>Values that entered the queue, including those handed straight to a waiting taker.</summary>
        Accepted: int64
        /// <summary>Values a full <c>Dropping</c> queue discarded.</summary>
        Dropped: int64
        /// <summary>Values a full <c>Sliding</c> queue evicted to make room.</summary>
        Evicted: int64
        /// <summary>Fibers suspended waiting to take.</summary>
        WaitingTakers: int
        /// <summary>Fibers suspended waiting to offer into a full <c>BackPressure</c> queue.</summary>
        WaitingOfferers: int
        /// <summary>Whether the queue has been shut down.</summary>
        IsShutdown: bool
    }

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
/// Suspended takers and suspended offerers are each served in FIFO order, and values leave in FIFO order even when
/// takers are interrupted: waiting takers are served one at a time, so a value given back by an interrupted taker
/// returns to the front before anything later has been taken.
/// </para>
/// </remarks>
/// <typeparam name="a">The type of the queued values.</typeparam>
type Dequeue<'a> internal (strategy: QueueStrategy) =
    let gate = obj ()

    member internal _.Gate = gate
    member internal _.Strategy = strategy
    member val internal Buffer = Deque<'a>()
    /// Suspended takers. Each is handed its whole batch at once, as a list.
    member val internal Takers = WaitList<'a list>(gate)
    /// The taker most recently handed a batch that has not yet resumed. While it is set, no other taker is served
    /// and no non-suspending take reads the buffer, so a batch given back by an interrupted taker returns to the front
    /// with nothing after it consumed: values leave in FIFO order even under interruption.
    member val internal PendingTaker: Waiter<'a list> voption = ValueNone with get, set
    member val internal Offerers = WaitList<'a>(gate)
    member val internal ShutdownSignal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    member val internal IsShut = false with get, set
    member val internal Accepted = 0L with get, set
    member val internal Dropped = 0L with get, set
    member val internal Evicted = 0L with get, set
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
    | Taken of 'a list
    | TakeSuspended of Waiter<'a list>
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

    /// Moves suspended offerers into freed buffer space in FIFO order.
    let private refillLocked (queue: Dequeue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) =
        while queue.Offerers.Count > 0 && hasRoom queue do
            queue.Buffer.PushBack(queue.Offerers.AcceptOldest wake)
            queue.Accepted <- queue.Accepted + 1L

    let private popUpTo (queue: Dequeue<'a>) (max: int) =
        [ for _ in 1 .. min max queue.Buffer.Count -> queue.Buffer.PopFront() ]

    /// Serves the oldest suspended taker once nothing is pending and enough values are buffered, then releases the
    /// remaining takers if the queue is shut down and drained. Takers are served one at a time; see PendingTaker.
    let private serveLocked (queue: Dequeue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) =
        if queue.PendingTaker.IsNone && queue.Takers.Count > 0 then
            let head = queue.Takers.Oldest
            let available = queue.Buffer.Count

            if available >= head.Min || (queue.IsShut && available > 0) then
                let batch = popUpTo queue head.Max
                queue.PendingTaker <- ValueSome(queue.Takers.CompleteOldest(batch, wake))
                refillLocked queue wake

        if queue.IsShut && queue.Buffer.Count = 0 && queue.PendingTaker.IsNone then
            queue.Takers.ShutDownAll wake

    /// Whether the value can enter now: there is room, or it completes the batch of the taker that is next in line.
    let canAcceptLocked (queue: Dequeue<'a>) =
        hasRoom queue
        || (queue.PendingTaker.IsNone
            && queue.Takers.Count > 0
            && queue.Buffer.Count + 1 >= queue.Takers.Oldest.Min)

    /// Whether a take that does not wait may read the buffer: no taker is waiting and no batch is pending, so it
    /// cannot overtake anyone.
    let private nobodyWaiting (queue: Dequeue<'a>) =
        queue.Takers.Count = 0 && queue.PendingTaker.IsNone

    let private deliverLocked (queue: Dequeue<'a>) (value: 'a) (wake: ResizeArray<Platform.Signal<unit>>) =
        queue.Accepted <- queue.Accepted + 1L
        queue.Buffer.PushBack value
        serveLocked queue wake

    /// Settles the pending batch of a taker that resumed with it, and serves the next taker.
    let private acknowledgeLocked (queue: Dequeue<'a>) (taker: Waiter<'a list>) (wake: ResizeArray<Platform.Signal<unit>>) =
        match queue.PendingTaker with
        | ValueSome pending when obj.ReferenceEquals(pending, taker) ->
            queue.PendingTaker <- ValueNone
            serveLocked queue wake
        | _ -> ()

    /// Returns a batch that a taker was handed in the same instant it was interrupted. Nothing after it has been
    /// consumed, so it goes back to the front in order, and the next taker is served.
    let private giveBackLocked
        (queue: Dequeue<'a>)
        (taker: Waiter<'a list>)
        (values: 'a list)
        (wake: ResizeArray<Platform.Signal<unit>>)
        =
        match queue.PendingTaker with
        | ValueSome pending when obj.ReferenceEquals(pending, taker) -> queue.PendingTaker <- ValueNone
        | _ -> ()

        for value in List.rev values do
            match queue.Strategy with
            // A full sliding queue would evict its oldest value next anyway, and the returned value is the oldest.
            | QueueStrategy.Sliding capacity when queue.Buffer.Count >= capacity -> queue.Evicted <- queue.Evicted + 1L
            // Lossless strategies may briefly exceed capacity rather than drop a value that was accepted.
            | _ -> queue.Buffer.PushFront value

        serveLocked queue wake

    let offerLocked (queue: Dequeue<'a>) (value: 'a) (wake: ResizeArray<Platform.Signal<unit>>) : OfferOutcome<'a> =
        if queue.IsShut then
            OfferRejected
        elif canAcceptLocked queue then
            deliverLocked queue value wake
            Accepted
        else
            match queue.Strategy with
            | QueueStrategy.BackPressure _
            | QueueStrategy.Unbounded -> OfferSuspended(queue.Offerers.Enqueue value)
            | QueueStrategy.Dropping _ ->
                queue.Dropped <- queue.Dropped + 1L
                Discarded
            | QueueStrategy.Sliding _ ->
                queue.Buffer.PopFront() |> ignore
                queue.Buffer.PushBack value
                queue.Accepted <- queue.Accepted + 1L
                queue.Evicted <- queue.Evicted + 1L
                serveLocked queue wake
                AcceptedEvicting

    /// Takes between <paramref name="min" /> and <paramref name="max" /> values at once, or waits in line for them.
    let takeLocked (queue: Dequeue<'a>) (min: int) (max: int) (wake: ResizeArray<Platform.Signal<unit>>) : TakeOutcome<'a> =
        let available = queue.Buffer.Count

        if nobodyWaiting queue && (available >= min || (queue.IsShut && available > 0)) then
            let batch = popUpTo queue max
            refillLocked queue wake
            Taken batch
        elif queue.IsShut && available = 0 && queue.PendingTaker.IsNone then
            Exhausted
        else
            let taker = queue.Takers.Enqueue []
            taker.Min <- min
            taker.Max <- max
            serveLocked queue wake
            TakeSuspended taker

    /// Takes up to <paramref name="max" /> buffered values without waiting, unless that would overtake a waiting taker.
    let takeUpToLocked (queue: Dequeue<'a>) (max: int) (wake: ResizeArray<Platform.Signal<unit>>) : 'a list =
        if nobodyWaiting queue then
            let taken = popUpTo queue max
            refillLocked queue wake
            taken
        else
            []

    let shutdown (queue: Dequeue<'a>) =
        let wake = ResizeArray()

        let changed =
            Platform.lock queue.Gate (fun () ->
                if queue.IsShut then
                    false
                else
                    queue.IsShut <- true
                    // Waiting takers are released only once the backlog has drained, so they receive it first.
                    serveLocked queue wake
                    queue.Offerers.ShutDownAll wake
                    wake.Add queue.ShutdownSignal
                    true)

        wakeAll wake

        if changed then
            queue.OnShutdown()

    /// Reads the queue's backlog and counters in one consistent snapshot.
    let stats (queue: Dequeue<'a>) : QueueStats =
        Platform.lock queue.Gate (fun () ->
            { Size = queue.Buffer.Count
              Capacity = queue.Capacity
              Accepted = queue.Accepted
              Dropped = queue.Dropped
              Evicted = queue.Evicted
              WaitingTakers = queue.Takers.Count
              WaitingOfferers = queue.Offerers.Count
              IsShutdown = queue.IsShut })

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

    /// Waits for a suspended taker's batch. A taker interrupted in the same instant it was handed its batch gives the
    /// batch back to the front, and one interrupted while waiting leaves its place to the next taker.
    let private awaitTaker
        (queue: Dequeue<'a>)
        (taker: Waiter<'a list>)
        (onShutdown: unit -> Execution<'result, 'error>)
        (onBatch: 'a list -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        WaitList.awaitWith
            queue.Takers
            taker
            (fun wake -> serveLocked queue wake)
            (ReturnHandover(fun batch wake -> giveBackLocked queue taker batch wake))
            (fun taker ->
                match taker.State with
                | Completed ->
                    let batch = taker.Value
                    taker.Value <- []
                    let wake = ResizeArray()
                    Platform.lock queue.Gate (fun () -> acknowledgeLocked queue taker wake)
                    wakeAll wake
                    onBatch batch
                | _ -> onShutdown ())
            cancellationToken

    /// Takes between <paramref name="min" /> and <paramref name="max" /> values, waiting in FIFO order among takers
    /// until <c>min</c> are available. <paramref name="onShutdown" /> runs once the queue is shut down and drained.
    let takeBatch
        (queue: Dequeue<'a>)
        (min: int)
        (max: int)
        (onShutdown: unit -> Execution<'result, 'error>)
        (onBatch: 'a list -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        let wake = ResizeArray()
        let outcome = Platform.lock queue.Gate (fun () -> takeLocked queue min max wake)
        wakeAll wake

        match outcome with
        | Taken batch -> onBatch batch
        | Exhausted -> onShutdown ()
        | TakeSuspended taker -> awaitTaker queue taker onShutdown onBatch cancellationToken

    /// Takes one value, suspending while the queue is empty. <paramref name="onShutdown" /> runs once the queue
    /// is shut down and drained.
    let take
        (queue: Dequeue<'a>)
        (onShutdown: unit -> Execution<'result, 'error>)
        (onValue: 'a -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        takeBatch queue 1 1 onShutdown (List.head >> onValue) cancellationToken

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
    /// batch limit. It takes nothing until <c>min</c> values are available and then takes the whole batch at once, so
    /// an interruption while it waits leaves every value in the queue. Takers are served in the order they started
    /// waiting, a single <c>take</c> and a <c>takeBetween</c> alike. After shutdown it returns the remaining values even
    /// if there are fewer than <c>min</c>, and is interrupted once nothing remains. <c>min</c> must be at least 1, no
    /// greater than <c>max</c>, and no greater than a bounded queue's capacity; otherwise the flow fails with a defect.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Wait for at least one sample, then write up to 500 in one batch.
    /// samples |&gt; Dequeue.takeBetween 1 500
    /// </code>
    /// </example>
    let takeBetween (min: int) (max: int) (queue: Dequeue<'a>) : Flow<'env, 'error, 'a list> =
        Flow(fun _ cancellationToken ->
            if min < 1 || max < min || queue.Capacity |> Option.exists (fun capacity -> min > capacity) then
                Execution.ofDie (
                    ArgumentOutOfRangeException(nameof min, "takeBetween needs 1 <= min <= max, and min <= capacity.")
                )
            else
                QueueCore.takeBatch queue min max (fun () -> Execution.ofCause Cause.Interrupt) Execution.ofValue cancellationToken)

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

    /// <summary>Reads the queue's backlog and counters in one consistent snapshot.</summary>
    /// <example>
    /// <code>
    /// flow {
    ///     let! stats = Dequeue.stats historian
    ///     if stats.Capacity |&gt; Option.exists (fun limit -&gt; stats.Size * 10 &gt;= limit * 8) then
    ///         do! raiseAlarm "historian buffer 80% full"
    /// }
    /// </code>
    /// </example>
    let stats (queue: Dequeue<'a>) : Flow<'env, 'error, QueueStats> =
        Flow(fun _ _ -> Execution.ofValue (QueueCore.stats queue))

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
