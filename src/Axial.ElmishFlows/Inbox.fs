namespace Axial.ElmishFlows

open System
open System.Threading
open Axial
open global.Elmish

/// <summary>One-way, drop-visible mail into an Elmish program: one queue, one reader, owned by a scope.</summary>
/// <remarks>
/// An inbox is a <c>Queue</c> with the doors that must not be opened at an <c>update</c> boundary closed: there is one
/// consumer, nothing can drain or shut it down from outside, and offers that never become messages are counted. Use it
/// for values that arrive from outside the program (callbacks, hosts, other libraries' events). Use <c>Queue</c>
/// directly for work distribution, fan-in or back-pressure between fibers.
/// </remarks>
[<Sealed>]
type Inbox<'value> internal (queue: Queue<'value>) =
    let mutable dropped = 0L
    member internal _.Queue = queue
    member internal _.CountDrop() = Interlocked.Increment(&dropped) |> ignore

    /// <summary>Values the mailbox could not keep: an offer refused while it was full, or the pending value a
    /// sliding mailbox evicted to take a newer one.</summary>
    member _.Dropped = Interlocked.Read(&dropped)

/// <summary>Constructs, fills and reads an <see cref="T:Axial.ElmishFlows.Inbox`1" />.</summary>
[<RequireQualifiedAccess>]
module Inbox =
    /// <summary>An inbox whose queue is shut down when the current scope closes, with the given strategy.</summary>
    /// <remarks>Build it in the runtime's layer or root scope: a layer's resources stay open until the runtime stops.</remarks>
    /// <param name="strategy">What happens when the mailbox is full: wait, slide, drop, or grow.</param>
    let scopedWith (strategy: QueueStrategy) : Flow<'env, Never, Inbox<'value>> =
        Queue.makeScoped strategy |> Flow.map (fun queue -> Inbox<'value> queue)

    /// <summary>A non-blocking inbox in which the newest pending value survives a burst; the oldest is dropped.</summary>
    let scopedSliding (capacity: int) : Flow<'env, Never, Inbox<'value>> =
        scopedWith (QueueStrategy.Sliding capacity)

    /// <summary>A non-blocking inbox in which the oldest pending value survives; a newer offer is dropped.</summary>
    let scopedDropping (capacity: int) : Flow<'env, Never, Inbox<'value>> =
        scopedWith (QueueStrategy.Dropping capacity)

    /// <summary>A lossless inbox in which an offer waits while the mailbox is full.</summary>
    /// <remarks>Do not offer to it from a <c>Cmd</c>: a command runs on the UI thread and would wait there. Offer from a
    /// fiber instead.</remarks>
    let scopedBounded (capacity: int) : Flow<'env, Never, Inbox<'value>> =
        scopedWith (QueueStrategy.BackPressure capacity)

    /// <summary>A lossless inbox that never waits; memory grows with the backlog.</summary>
    let scopedUnbounded () : Flow<'env, Never, Inbox<'value>> =
        scopedWith QueueStrategy.Unbounded

    /// <summary>An inbox shut down by the runtime instead of a scope, for a host that already holds the runtime.</summary>
    /// <remarks>Prefer <c>scopedWith</c> inside a layer or the root scope. Drops the newest offer with
    /// <c>QueueStrategy.Dropping</c>.</remarks>
    let create (capacity: int) (runtime: ElmishRuntime<'env>) : Inbox<'value> =
        if capacity <= 0 then invalidArg (nameof capacity) "Capacity must be positive"
        let inbox = Inbox<'value>(Queue.droppingNow capacity)
        runtime.RegisterShutdown(fun () -> Queue.shutdownNow inbox.Queue)
        inbox

    /// <summary>Offers a value; returns whether the mailbox took it, false when it is full or shut down.</summary>
    /// <remarks>A sliding mailbox takes every value, so it reports <c>true</c> and counts the pending value it evicted.
    /// </remarks>
    let post (value: 'value) (inbox: Inbox<'value>) =
        match Queue.tryOffer value inbox.Queue with
        | QueueTryOfferResult.Accepted -> true
        | QueueTryOfferResult.Evicted ->
            inbox.CountDrop()
            true
        | QueueTryOfferResult.Dropped
        | QueueTryOfferResult.Full ->
            inbox.CountDrop()
            false
        | _ -> false

    /// <summary>The same offer, as a command for <c>update</c> to return.</summary>
    let offer (value: 'value) (inbox: Inbox<'value>) : Cmd<'msg> = [ fun _ -> post value inbox |> ignore ]

    /// <summary>The mailbox as a stream: every accepted value, in the order it was offered.</summary>
    /// <remarks>Read it once: two readers split the mail between them, which is a queue, not a mailbox.</remarks>
    let stream (inbox: Inbox<'value>) : FlowStream<'env, 'error, 'value> =
        FlowStream.fromDequeue inbox.Queue

    /// <summary>Values the mailbox could not keep, whether refused or evicted by a sliding mailbox.</summary>
    let dropped (inbox: Inbox<'value>) = inbox.Dropped
