namespace Axial

open System

/// <summary>How one hub subscription buffers values its subscriber has not taken yet.</summary>
/// <remarks>
/// The strategy belongs to each subscription, so one publisher can feed a lossless consumer and a latest-value
/// consumer at the same time.
/// </remarks>
[<RequireQualifiedAccess>]
type SubscriberStrategy =
    /// <summary>Lossless. When the buffer is full, <c>Hub.publish</c> suspends until the subscriber makes room.</summary>
    | BackPressure of capacity: int
    /// <summary>When the buffer is full, the newest value is discarded for this subscriber.</summary>
    | Dropping of capacity: int
    /// <summary>When the buffer is full, the oldest buffered value is evicted for this subscriber.</summary>
    | Sliding of capacity: int
    /// <summary>Lossless and never suspends the publisher; memory grows with the subscriber's backlog.</summary>
    | Unbounded

/// <summary>How many subscriptions took a published value.</summary>
type PublishResult =
    {
        /// <summary>Subscriptions that accepted the value.</summary>
        Delivered: int
        /// <summary><c>Dropping</c> subscriptions that discarded the value because their buffer was full.</summary>
        Dropped: int
    }

/// <summary>A hub subscription: the buffer of values one subscriber has not taken yet.</summary>
/// <typeparam name="a">The type of the published values.</typeparam>
[<Sealed>]
type Subscription<'a> internal (queue: Queue<'a>) =
    member internal _.Queue = queue

    /// <summary>Describes the subscription without reading its contents.</summary>
    override _.ToString() = "Subscription"

/// <summary>Broadcasts every published value to every current subscription.</summary>
/// <remarks>Create one with <c>Hub.make</c>. Use a hub when there can be zero or many consumers.</remarks>
/// <typeparam name="a">The type of the published values.</typeparam>
[<Sealed>]
type Hub<'a> internal () =
    member val internal Gate = obj ()
    member val internal Subscriptions = ResizeArray<Subscription<'a>>()
    member val internal IsShut = false with get, set
    /// A publish holds this single permit from its first delivery to its last, so concurrent publishes cannot
    /// interleave and every subscriber observes the same order.
    member val internal PublishTurn = PermitQueue.create 1

    /// <summary>Describes the hub without reading its subscriptions.</summary>
    override _.ToString() = "Hub"

module internal HubCore =
    let isShut (hub: Hub<'a>) = Platform.lock hub.Gate (fun () -> hub.IsShut)

    let unsubscribe (hub: Hub<'a>) (subscription: Subscription<'a>) =
        Platform.lock hub.Gate (fun () ->
            let index = hub.Subscriptions.FindIndex(fun current -> obj.ReferenceEquals(current, subscription))
            if index >= 0 then hub.Subscriptions.RemoveAt index)

        // Shutting the subscription's queue also releases a publisher suspended on it.
        QueueCore.shutdown subscription.Queue

    /// Waits for the hub's publishing turn, withdrawing the wait if the publisher is interrupted.
    let acquireTurn (hub: Hub<'a>) cancellationToken : Execution<unit, 'error> =
        match PermitQueue.tryAcquire hub.PublishTurn with
        | None -> Execution.ofValue ()
        | Some waiter ->
            Execution.fold
                Execution.ofValue
                (fun cause ->
                    PermitQueue.withdraw hub.PublishTurn waiter
                    Execution.ofCause cause)
                (Platform.awaitSignal waiter.Signal cancellationToken)

    /// Delivers one value to every current subscription, suspending on full back-pressure subscriptions.
    /// Requires the publishing turn.
    let deliver (hub: Hub<'a>) (value: 'a) cancellationToken : Execution<PublishResult, 'error> =
        let targets =
            Platform.lock hub.Gate (fun () -> if hub.IsShut then None else Some(hub.Subscriptions.ToArray()))

        match targets with
        | None -> Execution.ofCause Cause.Interrupt
        | Some targets ->
            Execution.loop (0, { Delivered = 0; Dropped = 0 }) (fun (index, result) ->
                if index = targets.Length then
                    Execution.ofValue (Platform.Break result)
                else
                    let queue = targets[index].Queue
                    let wake = ResizeArray()
                    let outcome = Platform.lock queue.Gate (fun () -> QueueCore.offerLocked queue value wake)
                    QueueCore.wakeAll wake

                    match outcome with
                    | Accepted true ->
                        Execution.ofValue (Platform.Continue(index + 1, { result with Delivered = result.Delivered + 1 }))
                    | Accepted false ->
                        Execution.ofValue (Platform.Continue(index + 1, { result with Dropped = result.Dropped + 1 }))
                    // The subscription closed after the targets were read; it no longer receives values.
                    | OfferRejected -> Execution.ofValue (Platform.Continue(index + 1, result))
                    | OfferSuspended offerer ->
                        QueueCore.awaitOffer queue offerer cancellationToken
                        |> Execution.fold
                            (fun _ ->
                                Execution.ofValue (Platform.Continue(index + 1, { result with Delivered = result.Delivered + 1 })))
                            (fun cause ->
                                // A subscription that closed while the publisher waited on it releases the publisher,
                                // which moves on. Hub shutdown and publisher interruption end the publish.
                                if offerer.State = ShutDown && not (isShut hub) then
                                    Execution.ofValue (Platform.Continue(index + 1, result))
                                else
                                    Execution.ofCause cause))

    let publishAll (hub: Hub<'a>) (values: 'a array) cancellationToken : Execution<PublishResult, 'error> =
        acquireTurn hub cancellationToken
        |> Execution.bind (fun () ->
            Execution.loop (0, { Delivered = 0; Dropped = 0 }) (fun (index, total) ->
                if index = values.Length then
                    Execution.ofValue (Platform.Break total)
                else
                    deliver hub values[index] cancellationToken
                    |> Execution.map (fun result ->
                        Platform.Continue(
                            index + 1,
                            { Delivered = total.Delivered + result.Delivered
                              Dropped = total.Dropped + result.Dropped }
                        )))
            |> Execution.fold
                (fun result ->
                    PermitQueue.release hub.PublishTurn
                    Execution.ofValue result)
                (fun cause ->
                    PermitQueue.release hub.PublishTurn
                    Execution.ofCause cause))

/// <summary>Creates hubs, publishes to them, and subscribes to them.</summary>
/// <remarks>
/// <para>
/// A publish reaches every subscription that exists when it starts; a subscriber sees only values published after its
/// <c>subscribe</c> completes, so a late joiner that needs current state must get it from the application (for
/// example a snapshot plus sequence numbers). Publishes are serialized, so every subscriber observes values in the
/// same order even with concurrent publishers.
/// </para>
/// <para>
/// <c>Dropping</c>, <c>Sliding</c>, and <c>Unbounded</c> subscriptions never delay the publisher. A <c>BackPressure</c>
/// subscription with a full buffer suspends <c>publish</c> until it has room or its subscription closes. Lossless
/// delivery needs either back-pressure or unbounded memory, so size a lossless subscriber's buffer and watch
/// <c>Subscription.size</c> to raise an alarm before it fills.
/// </para>
/// <para>
/// Shutting a hub down shuts every subscription down with <c>Queue.shutdown</c> semantics: subscribers drain their
/// backlog, <c>FlowStream.fromSubscription</c> streams end normally, and later publishes are interrupted.
/// </para>
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Hub =
    /// <summary>Creates a hub with no subscriptions.</summary>
    let make () : Flow<'env, 'error, Hub<'a>> =
        Flow(fun _ _ -> Execution.ofValue (Hub<'a>()))

    /// <summary>Delivers a value to every current subscription.</summary>
    /// <remarks>
    /// Suspends while a <c>BackPressure</c> subscription is full. Interrupted if the hub is or becomes shut down. If
    /// the publisher is interrupted part way, subscriptions already reached keep the value.
    /// </remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (readings: Hub&lt;float&gt;) = Hub.make ()
    ///     return! readings |&gt; Hub.publish 21.5
    /// }
    /// </code>
    /// </example>
    let publish (value: 'a) (hub: Hub<'a>) : Flow<'env, 'error, PublishResult> =
        Flow(fun _ cancellationToken -> HubCore.publishAll hub [| value |] cancellationToken)

    /// <summary>Publishes values in order, with no other publish interleaved, and sums their results.</summary>
    let publishAll (values: 'a seq) (hub: Hub<'a>) : Flow<'env, 'error, PublishResult> =
        Flow(fun _ cancellationToken -> HubCore.publishAll hub (Seq.toArray values) cancellationToken)

    /// <summary>Subscribes to the hub with a buffering strategy. The subscription ends when the current scope closes.</summary>
    /// <remarks>
    /// Run <c>subscribe</c> inside <c>Flow.scoped</c> or an application root. Closing that scope removes the
    /// subscription and releases a publisher suspended on it. A non-positive capacity fails with a defect.
    /// </remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (readings: Hub&lt;float&gt;) = Hub.make ()
    ///     let! latest = readings |&gt; Hub.subscribe (SubscriberStrategy.Sliding 1)
    ///     do! readings |&gt; Hub.publishAll [ 20.0; 21.5 ] |&gt; Flow.ignore
    ///     return! Subscription.takeAll latest
    /// }
    /// |&gt; Flow.scoped
    /// </code>
    /// </example>
    let subscribe (strategy: SubscriberStrategy) (hub: Hub<'a>) : Flow<'env, 'error, Subscription<'a>> =
        Flow(fun _ _ ->
            let queueSettings =
                match strategy with
                | SubscriberStrategy.BackPressure capacity -> BackPressure, Some capacity
                | SubscriberStrategy.Dropping capacity -> Dropping, Some capacity
                | SubscriberStrategy.Sliding capacity -> Sliding, Some capacity
                | SubscriberStrategy.Unbounded -> BackPressure, None

            match queueSettings with
            | _, Some capacity when capacity <= 0 ->
                Execution.ofDie (ArgumentOutOfRangeException(nameof capacity, "Subscription capacity must be positive."))
            | queueStrategy, capacity ->
                let subscription = Subscription(QueueCore.create queueStrategy capacity)

                let added =
                    Platform.lock hub.Gate (fun () ->
                        if hub.IsShut then
                            false
                        else
                            hub.Subscriptions.Add subscription
                            true)

                if not added then
                    // A subscription to a shut-down hub is already ended, like the hub's other subscriptions.
                    QueueCore.shutdown subscription.Queue
                else
                    RuntimeState.current().Scope.AddFinalizer(fun _ ->
                        HubCore.unsubscribe hub subscription
                        Platform.completedDeed ())

                Execution.ofValue subscription)

    /// <summary>Returns the number of current subscriptions.</summary>
    let subscriberCount (hub: Hub<'a>) : Flow<'env, 'error, int> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock hub.Gate (fun () -> hub.Subscriptions.Count)))

    /// <summary>Shuts the hub and all of its subscriptions down. Calling it again has no effect.</summary>
    let shutdown (hub: Hub<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            let subscriptions =
                Platform.lock hub.Gate (fun () ->
                    hub.IsShut <- true
                    let current = hub.Subscriptions.ToArray()
                    hub.Subscriptions.Clear()
                    current)

            for subscription in subscriptions do
                QueueCore.shutdown subscription.Queue

            Execution.ofValue ())

    /// <summary>Returns whether the hub has been shut down.</summary>
    let isShutdown (hub: Hub<'a>) : Flow<'env, 'error, bool> =
        Flow(fun _ _ -> Execution.ofValue (HubCore.isShut hub))

/// <summary>Takes values from a hub subscription.</summary>
/// <remarks>These follow the matching <c>Queue</c> operations, including draining the backlog after shutdown.</remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Subscription =
    /// <summary>Removes the oldest value, suspending until one is published.</summary>
    let take (subscription: Subscription<'a>) : Flow<'env, 'error, 'a> = Queue.take subscription.Queue

    /// <summary>Removes the oldest value if one is available, without suspending.</summary>
    let poll (subscription: Subscription<'a>) : Flow<'env, 'error, 'a option> = Queue.poll subscription.Queue

    /// <summary>Removes up to <paramref name="max" /> available values in FIFO order, without suspending.</summary>
    let takeUpTo (max: int) (subscription: Subscription<'a>) : Flow<'env, 'error, 'a list> =
        Queue.takeUpTo max subscription.Queue

    /// <summary>Removes every available value in FIFO order, without suspending.</summary>
    let takeAll (subscription: Subscription<'a>) : Flow<'env, 'error, 'a list> = Queue.takeAll subscription.Queue

    /// <summary>Returns the number of values waiting for this subscriber, for high-watermark alarms.</summary>
    let size (subscription: Subscription<'a>) : Flow<'env, 'error, int> = Queue.size subscription.Queue
