namespace Axial

open System

/// <summary>What happened to one publish, counted over the subscriptions it reached.</summary>
type PublishResult =
    {
        /// <summary>Subscriptions that accepted the value, including sliding subscriptions that evicted to make room.</summary>
        Delivered: int
        /// <summary><c>Dropping</c> subscriptions that discarded the value because their buffer was full.</summary>
        Dropped: int
        /// <summary><c>Sliding</c> subscriptions that evicted their oldest value to accept this one.</summary>
        Evicted: int
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module PublishResult =
    /// <summary>The result of a publish that reached no subscription.</summary>
    let empty = { Delivered = 0; Dropped = 0; Evicted = 0 }

    /// <summary>Adds two results, for summing the publishes of a batch.</summary>
    let add (left: PublishResult) (right: PublishResult) =
        { Delivered = left.Delivered + right.Delivered
          Dropped = left.Dropped + right.Dropped
          Evicted = left.Evicted + right.Evicted }

/// <summary>Broadcasts every published value to every current subscription.</summary>
/// <remarks>Create one with <c>Hub.make</c>. Use a hub when there can be zero or many consumers.</remarks>
/// <typeparam name="a">The type of the published values.</typeparam>
[<Sealed>]
type Hub<'a> internal () =
    member val internal Gate = obj ()
    member val internal Subscriptions = ResizeArray<Dequeue<'a>>()
    member val internal IsShut = false with get, set
    member val internal ShutdownSignal: Platform.Signal<unit> = Platform.newSignal<unit> ()
    /// A publish holds this single permit from its first delivery to its last, so concurrent publishes cannot
    /// interleave and every subscriber observes the same order.
    member val internal PublishTurn = PermitQueue.create 1

    /// <summary>Describes the hub without reading its subscriptions.</summary>
    override _.ToString() = "Hub"

module internal HubCore =
    let isShut (hub: Hub<'a>) = Platform.lock hub.Gate (fun () -> hub.IsShut)

    let forget (hub: Hub<'a>) (subscription: Dequeue<'a>) =
        Platform.lock hub.Gate (fun () ->
            let index = hub.Subscriptions.FindIndex(fun current -> obj.ReferenceEquals(current, subscription))
            if index >= 0 then hub.Subscriptions.RemoveAt index)

    let targets (hub: Hub<'a>) =
        Platform.lock hub.Gate (fun () -> if hub.IsShut then None else Some(hub.Subscriptions.ToArray()))

    let private count (outcome: OfferOutcome<'a>) (result: PublishResult) =
        match outcome with
        | Accepted -> { result with Delivered = result.Delivered + 1 }
        | AcceptedEvicting ->
            { result with
                Delivered = result.Delivered + 1
                Evicted = result.Evicted + 1 }
        | Discarded -> { result with Dropped = result.Dropped + 1 }
        // The subscription ended after the targets were read; it no longer receives values.
        | OfferRejected
        | OfferSuspended _ -> result

    /// Delivers one value to every current subscription in subscription order, suspending on each full
    /// back-pressure subscription. Requires the publishing turn.
    let deliver (hub: Hub<'a>) (value: 'a) cancellationToken : Execution<PublishResult, 'error> =
        match targets hub with
        | None -> Execution.ofCause Cause.Interrupt
        | Some targets ->
            Execution.loop (0, PublishResult.empty) (fun (index, result) ->
                if index = targets.Length then
                    Execution.ofValue (Platform.Break result)
                else
                    let queue = targets[index]
                    let wake = ResizeArray()
                    let outcome = Platform.lock queue.Gate (fun () -> QueueCore.offerLocked queue value wake)
                    QueueCore.wakeAll wake

                    match outcome with
                    | OfferSuspended offerer ->
                        QueueCore.awaitOffer queue offerer cancellationToken
                        |> Execution.fold
                            (fun () ->
                                Execution.ofValue (Platform.Continue(index + 1, count Accepted result)))
                            (fun cause ->
                                // A subscription that closed while the publisher waited on it releases the publisher,
                                // which moves on. Hub shutdown and publisher interruption end the publish.
                                if offerer.State = ShutDown && not (isShut hub) then
                                    Execution.ofValue (Platform.Continue(index + 1, result))
                                else
                                    Execution.ofCause cause)
                    | outcome -> Execution.ofValue (Platform.Continue(index + 1, count outcome result)))

    let withTurn (hub: Hub<'a>) (run: unit -> Execution<'value, 'error>) : Execution<'value, 'error> =
        Execution.fold
            (fun value ->
                PermitQueue.release hub.PublishTurn
                Execution.ofValue value)
            (fun cause ->
                PermitQueue.release hub.PublishTurn
                Execution.ofCause cause)
            (run ())

    let publishAll (hub: Hub<'a>) (values: 'a array) cancellationToken : Execution<PublishResult, 'error> =
        PermitQueue.acquire hub.PublishTurn cancellationToken
        |> Execution.bind (fun () ->
            withTurn hub (fun () ->
                Execution.loop (0, PublishResult.empty) (fun (index, total) ->
                    if index = values.Length then
                        Execution.ofValue (Platform.Break total)
                    else
                        deliver hub values[index] cancellationToken
                        |> Execution.map (fun result -> Platform.Continue(index + 1, PublishResult.add total result)))))

    /// Publishes only if it can finish without waiting: the publishing turn is free and no back-pressure
    /// subscription is full. Holding the turn means no other publisher can fill a subscription between the check
    /// and the delivery, and consumers only make room.
    let tryPublish (hub: Hub<'a>) (value: 'a) : Execution<PublishResult option, 'error> =
        if not (PermitQueue.tryAcquireNow hub.PublishTurn) then
            Execution.ofValue None
        else
            withTurn hub (fun () ->
                match targets hub with
                | None -> Execution.ofCause Cause.Interrupt
                | Some targets ->
                    let full (queue: Dequeue<'a>) =
                        Platform.lock queue.Gate (fun () ->
                            match queue.Strategy with
                            | QueueStrategy.BackPressure capacity ->
                                not queue.IsShut && queue.Takers.Count = 0 && queue.Buffer.Count >= capacity
                            | _ -> false)

                    if targets |> Array.exists full then
                        Execution.ofValue None
                    else
                        deliver hub value Threading.CancellationToken.None |> Execution.map Some)

    let shutdown (hub: Hub<'a>) =
        let subscriptions =
            Platform.lock hub.Gate (fun () ->
                if hub.IsShut then
                    [||]
                else
                    hub.IsShut <- true
                    let current = hub.Subscriptions.ToArray()
                    hub.Subscriptions.Clear()
                    current)

        for subscription in subscriptions do
            QueueCore.shutdown subscription

        Platform.resolveSignal hub.ShutdownSignal () |> ignore

/// <summary>Creates hubs, publishes to them, and subscribes to them.</summary>
/// <remarks>
/// <para>
/// A publish reaches every subscription that exists when it starts; a subscriber sees only values published after its
/// <c>subscribe</c> completes, so a late joiner that needs current state must get it elsewhere, for example from a
/// <c>SubscriptionRef</c>. Publishes are serialized, so every subscriber observes values in the same order even with
/// concurrent publishers.
/// </para>
/// <para>
/// <c>Dropping</c>, <c>Sliding</c>, and <c>Unbounded</c> subscriptions never delay the publisher. A <c>BackPressure</c>
/// subscription with a full buffer suspends <c>publish</c> until it has room or its subscription ends. While the
/// publisher waits, no later value reaches any subscriber, lossy ones included: keeping one order for every subscriber
/// means a stalled lossless subscriber stalls the feed. Size a lossless subscriber's buffer for the bursts you expect,
/// watch <c>Dequeue.size</c> to raise an alarm before it fills, and use <c>Hub.tryPublish</c> where the publisher must
/// never wait.
/// </para>
/// <para>
/// Shutting a hub down shuts every subscription down with <c>Dequeue.shutdown</c> semantics: subscribers drain their
/// backlog, streams over subscriptions end normally, and later publishes are interrupted.
/// </para>
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Hub =
    /// <summary>Creates a hub with no subscriptions.</summary>
    let make () : Flow<'env, 'error, Hub<'a>> =
        Flow(fun _ _ -> Execution.ofValue (Hub<'a>()))

    /// <summary>Creates a hub that is shut down when the current scope closes.</summary>
    /// <remarks>Closing the scope lets every subscriber drain its backlog and ends streams over the hub normally.</remarks>
    let makeScoped () : Flow<'env, 'error, Hub<'a>> =
        Flow(fun _ _ ->
            let hub = Hub<'a>()

            RuntimeState.current().Scope.AddFinalizer(fun _ ->
                HubCore.shutdown hub
                Platform.completedDeed ())

            Execution.ofValue hub)

    /// <summary>Delivers a value to every current subscription.</summary>
    /// <remarks>
    /// Suspends while a <c>BackPressure</c> subscription is full, and is interrupted if the hub is or becomes shut
    /// down. If the publisher is interrupted part way, the subscriptions already reached keep the value, so publishing
    /// the same value again would deliver it twice to them; bound the wait with <c>Hub.tryPublish</c> instead of a
    /// timeout when duplicates matter.
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

    /// <summary>Publishes a value only if that needs no waiting; returns <c>None</c> without publishing otherwise.</summary>
    /// <remarks>
    /// Returns <c>None</c> when another publish is in progress or a <c>BackPressure</c> subscription is full. The
    /// check and the delivery are atomic, so a value is delivered to every subscription or to none. A control loop
    /// that must never stall uses it and decides what to do with a value the historian cannot take yet.
    /// </remarks>
    let tryPublish (value: 'a) (hub: Hub<'a>) : Flow<'env, 'error, PublishResult option> =
        Flow(fun _ _ -> HubCore.tryPublish hub value)

    /// <summary>Subscribes to the hub with a buffering strategy. The subscription ends when the current scope closes.</summary>
    /// <remarks>
    /// Run <c>subscribe</c> inside <c>Flow.scoped</c>, a forked fiber, or an application root: a forked fiber's
    /// subscription ends with the fiber. Ending the subscription, by closing its scope or with
    /// <c>Dequeue.shutdown</c>, removes it from the hub and releases a publisher waiting on it. To subscribe for the
    /// life of a stream, use <c>FlowStream.fromHub</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (readings: Hub&lt;float&gt;) = Hub.make ()
    ///     let! latest = readings |&gt; Hub.subscribe (QueueStrategy.Sliding 1)
    ///     do! readings |&gt; Hub.publishAll [ 20.0; 21.5 ] |&gt; Flow.ignore
    ///     return! Dequeue.takeAll latest
    /// }
    /// |&gt; Flow.scoped
    /// </code>
    /// </example>
    let subscribe (strategy: QueueStrategy) (hub: Hub<'a>) : Flow<'env, 'error, Dequeue<'a>> =
        Flow(fun _ _ ->
            match QueueCore.validate strategy with
            | Some error -> Execution.ofDie error
            | None ->
                let subscription = Dequeue<'a>(strategy)
                let scope = RuntimeState.current().Scope

                // Register the release before joining the hub, so a subscription can never be in the hub without
                // an owner that will end it.
                let key =
                    scope.Register(fun _ ->
                        QueueCore.shutdown subscription
                        Platform.completedDeed ())

                // An early unsubscribe also drops the scope registration, so a long-lived scope does not retain it.
                subscription.OnShutdown <-
                    fun () ->
                        HubCore.forget hub subscription
                        scope.Unregister key

                let added =
                    Platform.lock hub.Gate (fun () ->
                        if hub.IsShut then
                            false
                        else
                            hub.Subscriptions.Add subscription
                            true)

                if not added then
                    // A subscription to a shut-down hub is already ended, like the hub's other subscriptions.
                    QueueCore.shutdown subscription

                Execution.ofValue subscription)

    /// <summary>Returns the number of current subscriptions.</summary>
    let subscriberCount (hub: Hub<'a>) : Flow<'env, 'error, int> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock hub.Gate (fun () -> hub.Subscriptions.Count)))

    /// <summary>Shuts the hub and all of its subscriptions down. Calling it again has no effect.</summary>
    let shutdown (hub: Hub<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            HubCore.shutdown hub
            Execution.ofValue ())

    /// <summary>Returns whether the hub has been shut down.</summary>
    let isShutdown (hub: Hub<'a>) : Flow<'env, 'error, bool> =
        Flow(fun _ _ -> Execution.ofValue (HubCore.isShut hub))

    /// <summary>Suspends until the hub is shut down.</summary>
    let awaitShutdown (hub: Hub<'a>) : Flow<'env, 'error, unit> =
        Flow(fun _ cancellationToken -> Platform.awaitSignal hub.ShutdownSignal cancellationToken)
