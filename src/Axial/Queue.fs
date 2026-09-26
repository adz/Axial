namespace Axial

open System

type internal WaiterState =
    | Waiting
    | Completed
    | Cancelled
    | ShutDown

/// A suspended taker or offerer. Its state changes only under the owning queue's gate, so an interrupted waiter
/// and a producer or consumer completing it cannot both win.
type internal QueueWaiter<'a>(value: 'a) =
    member val Signal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    member val Value = value with get, set
    member val State = Waiting with get, set

type internal QueueStrategy =
    | BackPressure
    | Dropping
    | Sliding

/// <summary>
/// An asynchronous FIFO queue for handing values between fibers, with an overflow strategy chosen at creation.
/// </summary>
/// <remarks>
/// Create one with <c>Queue.bounded</c>, <c>Queue.dropping</c>, <c>Queue.sliding</c>, or <c>Queue.unbounded</c>.
/// A queue is meant for one logical consumer; use <c>Hub</c> when zero or many consumers each need every value.
/// </remarks>
/// <typeparam name="a">The type of the queued values.</typeparam>
[<Sealed>]
type Queue<'a> internal (strategy: QueueStrategy, capacity: int option) =
    member val internal Gate = obj ()
    member val internal Buffer = Deque<'a>()
    member val internal Takers = Deque<QueueWaiter<'a>>()
    member val internal Offerers = Deque<QueueWaiter<'a>>()
    member val internal ShutdownSignal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    member val internal IsShut = false with get, set
    member internal _.Strategy = strategy
    member internal _.Capacity = capacity

    /// <summary>Describes the queue without reading its contents.</summary>
    override _.ToString() =
        match capacity with
        | Some limit -> $"Queue(capacity {limit})"
        | None -> "Queue(unbounded)"

type internal OfferOutcome<'a> =
    | Accepted of bool
    | OfferSuspended of QueueWaiter<'a>
    | OfferRejected

type internal TakeOutcome<'a> =
    | Taken of 'a
    | TakeSuspended of QueueWaiter<'a>
    | Exhausted

/// Queue operations over raw executions. Functions named <c>...Locked</c> require the caller to hold the gate and
/// add the signals they complete to <c>wake</c>; callers resolve those after releasing the gate.
module internal QueueCore =
    let create strategy capacity : Queue<'a> = Queue<'a>(strategy, capacity)

    let wakeAll (wake: ResizeArray<Platform.Signal<unit>>) =
        for signal in wake do
            Platform.resolveSignal signal () |> ignore

    let private hasRoom (queue: Queue<'a>) =
        match queue.Capacity with
        | Some limit -> queue.Buffer.Count < limit
        | None -> true

    /// Hands a value to the oldest waiting taker, or buffers it. Takers wait only while the buffer is empty.
    let private deliverLocked (queue: Queue<'a>) (value: 'a) (wake: ResizeArray<Platform.Signal<unit>>) =
        if queue.Takers.Count > 0 then
            let taker = queue.Takers.PopFront()
            taker.Value <- value
            taker.State <- Completed
            wake.Add taker.Signal
        else
            queue.Buffer.PushBack value

    /// Moves suspended offerers into freed buffer space in FIFO order.
    let private refillLocked (queue: Queue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) =
        while queue.Offerers.Count > 0 && hasRoom queue do
            let offerer = queue.Offerers.PopFront()
            queue.Buffer.PushBack offerer.Value
            offerer.Value <- Unchecked.defaultof<'a>
            offerer.State <- Completed
            wake.Add offerer.Signal

    let offerLocked (queue: Queue<'a>) (value: 'a) (wake: ResizeArray<Platform.Signal<unit>>) : OfferOutcome<'a> =
        if queue.IsShut then
            OfferRejected
        elif queue.Takers.Count > 0 || hasRoom queue then
            deliverLocked queue value wake
            Accepted true
        else
            match queue.Strategy with
            | BackPressure ->
                let offerer = QueueWaiter value
                queue.Offerers.PushBack offerer
                OfferSuspended offerer
            | Dropping -> Accepted false
            | Sliding ->
                queue.Buffer.PopFront() |> ignore
                queue.Buffer.PushBack value
                Accepted true

    let takeLocked (queue: Queue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) : TakeOutcome<'a> =
        if queue.Buffer.Count > 0 then
            let value = queue.Buffer.PopFront()
            refillLocked queue wake
            Taken value
        elif queue.IsShut then
            Exhausted
        else
            let taker = QueueWaiter Unchecked.defaultof<'a>
            queue.Takers.PushBack taker
            TakeSuspended taker

    let pollLocked (queue: Queue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) : 'a option =
        if queue.Buffer.Count > 0 then
            let value = queue.Buffer.PopFront()
            refillLocked queue wake
            Some value
        else
            None

    let takeUpToLocked (queue: Queue<'a>) (max: int) (wake: ResizeArray<Platform.Signal<unit>>) : 'a list =
        let taken = ResizeArray<'a>()

        while taken.Count < max && queue.Buffer.Count > 0 do
            taken.Add(queue.Buffer.PopFront())

        refillLocked queue wake
        List.ofSeq taken

    let shutdownLocked (queue: Queue<'a>) (wake: ResizeArray<Platform.Signal<unit>>) =
        if not queue.IsShut then
            queue.IsShut <- true

            for waiter in queue.Takers.Drain() @ queue.Offerers.Drain() do
                waiter.Value <- Unchecked.defaultof<'a>
                waiter.State <- ShutDown
                wake.Add waiter.Signal

            wake.Add queue.ShutdownSignal

    let shutdown (queue: Queue<'a>) =
        let wake = ResizeArray()
        Platform.lock queue.Gate (fun () -> shutdownLocked queue wake)
        wakeAll wake

    /// Waits for a suspended offer. An offer interrupted before a taker accepted its value is withdrawn and never
    /// enqueued; one accepted in the same instant as the interruption has already happened and reports success.
    let awaitOffer (queue: Queue<'a>) (offerer: QueueWaiter<'a>) cancellationToken : Execution<bool, 'error> =
        let settled () =
            match offerer.State with
            | Completed -> Execution.ofValue true
            | _ -> Execution.ofCause Cause.Interrupt

        Execution.fold
            (fun () -> settled ())
            (fun _ ->
                Platform.lock queue.Gate (fun () ->
                    if offerer.State = Waiting then
                        offerer.State <- Cancelled
                        offerer.Value <- Unchecked.defaultof<'a>
                        queue.Offerers.RemoveReference offerer |> ignore)

                settled ())
            (Platform.awaitSignal offerer.Signal cancellationToken)

    /// Waits for a suspended take. A taker interrupted after a value was handed to it gives that value back to
    /// the next taker or the front of the buffer, so interruption never loses an element.
    let awaitTake
        (queue: Queue<'a>)
        (taker: QueueWaiter<'a>)
        (onShutdown: unit -> Execution<'result, 'error>)
        (onValue: 'a -> Execution<'result, 'error>)
        cancellationToken
        : Execution<'result, 'error> =
        Execution.fold
            (fun () ->
                match taker.State with
                | Completed ->
                    let value = taker.Value
                    taker.Value <- Unchecked.defaultof<'a>
                    onValue value
                | _ -> onShutdown ())
            (fun cause ->
                let wake = ResizeArray()

                Platform.lock queue.Gate (fun () ->
                    match taker.State with
                    | Waiting ->
                        taker.State <- Cancelled
                        queue.Takers.RemoveReference taker |> ignore
                    | Completed ->
                        taker.State <- Cancelled
                        let value = taker.Value
                        taker.Value <- Unchecked.defaultof<'a>

                        if queue.Takers.Count > 0 then
                            let next = queue.Takers.PopFront()
                            next.Value <- value
                            next.State <- Completed
                            wake.Add next.Signal
                        else
                            // The value was accepted when the buffer was empty, so it belongs at the front.
                            // This can briefly exceed capacity by one element; no accepted value is dropped.
                            queue.Buffer.PushFront value
                    | Cancelled
                    | ShutDown -> ())

                wakeAll wake
                Execution.ofCause cause)
            (Platform.awaitSignal taker.Signal cancellationToken)

    /// Takes one value, suspending while the queue is empty. <paramref name="onShutdown" /> runs once the queue
    /// is shut down and drained.
    let take
        (queue: Queue<'a>)
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
        | TakeSuspended taker -> awaitTake queue taker onShutdown onValue cancellationToken

/// <summary>Creates, feeds, drains, and shuts down <see cref="T:Axial.Queue`1" /> values.</summary>
/// <remarks>
/// <para>
/// Shutdown ends a queue without discarding its backlog. Fibers suspended in <c>take</c> or <c>offer</c> complete as
/// interruptions, later offers are interrupted, and later takes return the remaining elements before they are
/// interrupted. A consumer loop can therefore finish the backlog after shutdown, which lets a writer flush when an
/// application stops. <c>FlowStream.fromQueue</c> treats shutdown plus an empty buffer as the normal end of the
/// stream.
/// </para>
/// <para>
/// Interrupting a suspended <c>take</c> never loses an element, and interrupting a suspended <c>offer</c> never
/// enqueues its value. Suspended takers and suspended offerers are each served in FIFO order.
/// </para>
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Queue =
    let private make strategy (capacity: int) : Flow<'env, 'error, Queue<'a>> =
        Flow(fun _ _ ->
            if capacity <= 0 then
                Execution.ofDie (ArgumentOutOfRangeException(nameof capacity, "Queue capacity must be positive."))
            else
                Execution.ofValue (QueueCore.create strategy (Some capacity)))

    /// <summary>Creates a queue whose <c>offer</c> suspends while it holds <paramref name="capacity" /> values.</summary>
    /// <remarks>A non-positive capacity fails the returned flow with a defect.</remarks>
    /// <example><code>Queue.bounded 64</code></example>
    let bounded (capacity: int) : Flow<'env, 'error, Queue<'a>> = make BackPressure capacity

    /// <summary>Creates a queue whose <c>offer</c> discards the new value and returns <c>false</c> when full.</summary>
    /// <remarks>A non-positive capacity fails the returned flow with a defect.</remarks>
    let dropping (capacity: int) : Flow<'env, 'error, Queue<'a>> = make Dropping capacity

    /// <summary>Creates a queue whose <c>offer</c> evicts the oldest value when full and returns <c>true</c>.</summary>
    /// <remarks>A non-positive capacity fails the returned flow with a defect.</remarks>
    let sliding (capacity: int) : Flow<'env, 'error, Queue<'a>> = make Sliding capacity

    /// <summary>Creates a queue that never rejects or suspends an <c>offer</c>.</summary>
    /// <remarks>Memory grows with the backlog; prefer a bounded queue unless producers are otherwise limited.</remarks>
    let unbounded () : Flow<'env, 'error, Queue<'a>> =
        Flow(fun _ _ -> Execution.ofValue (QueueCore.create BackPressure None))

    /// <summary>Creates a bounded queue that is shut down when the current scope closes.</summary>
    /// <remarks>
    /// Use it inside <c>Flow.scoped</c> or an application root. Closing the scope interrupts fibers suspended on
    /// the queue and ends any <c>FlowStream.fromQueue</c> consumer after it drains the backlog.
    /// </remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (jobs: Queue&lt;string&gt;) = Queue.boundedScoped 64
    ///     let! _ = jobs |&gt; FlowStream.fromQueue |&gt; FlowStream.runForEach (printfn "%s") |&gt; Flow.fork
    ///     do! jobs |&gt; Queue.offer "first job" |&gt; Flow.ignore
    /// }
    /// |&gt; Flow.scoped
    /// </code>
    /// </example>
    let boundedScoped (capacity: int) : Flow<'env, 'error, Queue<'a>> =
        Flow(fun _ _ ->
            if capacity <= 0 then
                Execution.ofDie (ArgumentOutOfRangeException(nameof capacity, "Queue capacity must be positive."))
            else
                let queue = QueueCore.create BackPressure (Some capacity)

                RuntimeState.current().Scope.AddFinalizer(fun _ ->
                    QueueCore.shutdown queue
                    Platform.completedDeed ())

                Execution.ofValue queue)

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
            let wake = ResizeArray()
            let outcome = Platform.lock queue.Gate (fun () -> QueueCore.offerLocked queue value wake)
            QueueCore.wakeAll wake

            match outcome with
            | Accepted accepted -> Execution.ofValue accepted
            | OfferRejected -> Execution.ofCause Cause.Interrupt
            | OfferSuspended offerer -> QueueCore.awaitOffer queue offerer cancellationToken)

    /// <summary>Adds values in order according to the queue's strategy.</summary>
    /// <returns>
    /// <c>true</c> when every value was accepted. A dropping queue accepts values while it has room and returns
    /// <c>false</c> if it discarded any. A bounded queue suspends as needed; values from other producers may be
    /// interleaved between the batch's values, but the batch's own order is kept. If the flow is interrupted part
    /// way, the values already accepted stay in the queue.
    /// </returns>
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
                        | Accepted wasAccepted ->
                            accepted.Value <- accepted.Value && wasAccepted
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
                        |> Execution.map (fun _ -> Platform.Continue(index.Value, accepted.Value))))

    /// <summary>Removes the oldest value, suspending until one is available.</summary>
    /// <remarks>After shutdown this returns the remaining values, then is interrupted.</remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (jobs: Queue&lt;string&gt;) = Queue.bounded 8
    ///     do! jobs |&gt; Queue.offer "job" |&gt; Flow.ignore
    ///     return! Queue.take jobs
    /// }
    /// </code>
    /// </example>
    let take (queue: Queue<'a>) : Flow<'env, 'error, 'a> =
        Flow(fun _ cancellationToken ->
            QueueCore.take queue (fun () -> Execution.ofCause Cause.Interrupt) Execution.ofValue cancellationToken)

    /// <summary>Removes the oldest value if one is available, without suspending.</summary>
    let poll (queue: Queue<'a>) : Flow<'env, 'error, 'a option> =
        Flow(fun _ _ ->
            let wake = ResizeArray()
            let value = Platform.lock queue.Gate (fun () -> QueueCore.pollLocked queue wake)
            QueueCore.wakeAll wake
            Execution.ofValue value)

    /// <summary>Removes up to <paramref name="max" /> available values in FIFO order, without suspending.</summary>
    /// <remarks>Returns an empty list when nothing is available. A negative maximum fails with a defect.</remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (samples: Queue&lt;float&gt;) = Queue.unbounded ()
    ///     do! samples |&gt; Queue.offerAll [ 1.0; 2.0; 3.0 ] |&gt; Flow.ignore
    ///     return! samples |&gt; Queue.takeUpTo 500
    /// }
    /// </code>
    /// </example>
    let takeUpTo (max: int) (queue: Queue<'a>) : Flow<'env, 'error, 'a list> =
        Flow(fun _ _ ->
            if max < 0 then
                Execution.ofDie (ArgumentOutOfRangeException(nameof max, "Maximum cannot be negative."))
            else
                let wake = ResizeArray()
                let values = Platform.lock queue.Gate (fun () -> QueueCore.takeUpToLocked queue max wake)
                QueueCore.wakeAll wake
                Execution.ofValue values)

    /// <summary>Removes every available value in FIFO order, without suspending.</summary>
    let takeAll (queue: Queue<'a>) : Flow<'env, 'error, 'a list> =
        Flow(fun _ _ ->
            let wake = ResizeArray()
            let values = Platform.lock queue.Gate (fun () -> QueueCore.takeUpToLocked queue Int32.MaxValue wake)
            QueueCore.wakeAll wake
            Execution.ofValue values)

    /// <summary>Returns the number of buffered values.</summary>
    let size (queue: Queue<'a>) : Flow<'env, 'error, int> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock queue.Gate (fun () -> queue.Buffer.Count)))

    /// <summary>Returns the queue's capacity, or <c>None</c> for an unbounded queue.</summary>
    let capacity (queue: Queue<'a>) : int option = queue.Capacity

    /// <summary>Shuts the queue down. Calling it again has no effect.</summary>
    let shutdown (queue: Queue<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            QueueCore.shutdown queue
            Execution.ofValue ())

    /// <summary>Returns whether the queue has been shut down.</summary>
    let isShutdown (queue: Queue<'a>) : Flow<'env, 'error, bool> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock queue.Gate (fun () -> queue.IsShut)))

    /// <summary>Suspends until the queue is shut down.</summary>
    let awaitShutdown (queue: Queue<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken -> Platform.awaitSignal queue.ShutdownSignal cancellationToken)
