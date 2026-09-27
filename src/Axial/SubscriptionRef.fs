namespace Axial.State

open Axial

/// <summary>
/// A mutable reference whose changes can be streamed: each stream starts with the current value and then receives every
/// later update, with no gap and no duplicate between the two.
/// </summary>
/// <remarks>
/// Use it for state that consumers join at different times, such as the latest reading shown by a display that opens
/// after the feed started. A plain <see cref="T:Axial.Hub`1" /> gives a late subscriber only later values; a
/// <c>SubscriptionRef</c> also gives it the value current when it joined.
/// </remarks>
/// <typeparam name="a">The type of the stored value.</typeparam>
[<Sealed>]
type SubscriptionRef<'a> internal (initial: 'a) =
    member val internal Gate = obj ()
    member val internal Current = initial with get, set
    member val internal Hub = Hub<'a>()

    /// <summary>Describes the reference without reading its value.</summary>
    override _.ToString() = "SubscriptionRef"

/// <summary>Creates, reads, updates, and streams <see cref="T:Axial.State.SubscriptionRef`1" /> values.</summary>
/// <remarks>
/// <para>
/// Updates are serialized with the stream subscriptions: a <c>changes</c> stream reads the current value and subscribes
/// while no update is in progress, so it never misses an update made after that value, and never sees one twice.
/// </para>
/// <para>
/// An update is delivered to every <c>changes</c> stream before the next update starts. A stream using a full
/// <c>QueueStrategy.BackPressure</c> buffer therefore makes updates wait for it, as a hub publisher does.
/// </para>
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module SubscriptionRef =
    let private write (update: 'a -> 'result * 'a) (reference: SubscriptionRef<'a>) : Flow<'env, 'error, 'result> =
        Flow(fun _ cancellationToken ->
            PermitQueue.acquire reference.Hub.PublishTurn cancellationToken
            |> Execution.bind (fun () ->
                HubCore.withTurn reference.Hub (fun () ->
                    let result, next =
                        Platform.lock reference.Gate (fun () ->
                            let result, next = update reference.Current
                            reference.Current <- next
                            result, next)

                    HubCore.deliver reference.Hub next cancellationToken |> Execution.map (fun _ -> result))))

    /// <summary>Creates a reference holding <paramref name="value" />.</summary>
    /// <example>
    /// <code>
    /// open Axial.State
    ///
    /// let temperature : Flow&lt;unit, Never, SubscriptionRef&lt;float&gt;&gt; = SubscriptionRef.make 20.0
    /// </code>
    /// </example>
    let make (value: 'a) : Flow<'env, 'error, SubscriptionRef<'a>> =
        Flow(fun _ _ -> Execution.ofValue (SubscriptionRef<'a>(value)))

    /// <summary>Reads the current value.</summary>
    let get (reference: SubscriptionRef<'a>) : Flow<'env, 'error, 'a> =
        Flow(fun _ _ -> Execution.ofValue (Platform.lock reference.Gate (fun () -> reference.Current)))

    /// <summary>Replaces the value and delivers it to every <c>changes</c> stream.</summary>
    /// <example><code>temperature |&gt; SubscriptionRef.set 21.5</code></example>
    let set (value: 'a) (reference: SubscriptionRef<'a>) : Flow<'env, 'error, unit> =
        write (fun _ -> (), value) reference

    /// <summary>Applies a pure function to the value and delivers the result to every <c>changes</c> stream.</summary>
    let update (mapping: 'a -> 'a) (reference: SubscriptionRef<'a>) : Flow<'env, 'error, unit> =
        write (fun current -> (), mapping current) reference

    /// <summary>Computes a result and a new value together, stores the new value, and returns the result.</summary>
    /// <remarks>The function returns <c>(result, newValue)</c>, the same order as <c>Ref.modify</c>.</remarks>
    let modify (mapping: 'a -> 'result * 'a) (reference: SubscriptionRef<'a>) : Flow<'env, 'error, 'result> =
        write mapping reference

    /// <summary>
    /// Streams the current value followed by every later update, buffered for this stream by <paramref name="strategy" />.
    /// </summary>
    /// <remarks>
    /// The stream subscribes when it starts and unsubscribes when it ends. It does not end on its own; stop it with
    /// <c>FlowStream.take</c>, by interrupting its consumer, or by closing the consumer's scope. Use
    /// <c>QueueStrategy.Sliding 1</c> for a consumer that only needs the latest value, and a lossless strategy for one
    /// that must see every change.
    /// </remarks>
    /// <example>
    /// <code>
    /// temperature
    /// |&gt; SubscriptionRef.changes (QueueStrategy.Sliding 1)
    /// |&gt; FlowStream.runForEach (printfn "%.1f")
    /// </code>
    /// </example>
    let changes (strategy: QueueStrategy) (reference: SubscriptionRef<'a>) : FlowStream<'env, 'error, 'a> =
        FlowStream(fun environment cancellationToken ->
            PermitQueue.acquire reference.Hub.PublishTurn cancellationToken
            |> Execution.bind (fun () ->
                HubCore.withTurn reference.Hub (fun () ->
                    // Holding the update turn: no update can land between reading the value and subscribing.
                    Flow.invoke (Hub.subscribe strategy reference.Hub) environment cancellationToken
                    |> Execution.map (fun subscription ->
                        let current = Platform.lock reference.Gate (fun () -> reference.Current)
                        let wake = ResizeArray()

                        Platform.lock subscription.Gate (fun () ->
                            QueueCore.offerLocked subscription current wake |> ignore)

                        QueueCore.wakeAll wake
                        subscription)))
            |> Execution.bind (fun subscription ->
                let (FlowStream pull) = FlowStream.fromDequeue subscription
                pull environment cancellationToken))
