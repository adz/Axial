namespace Axial.ElmishFlows

open System
open System.Threading
open Axial
open global.Elmish

[<Sealed>]
type Inbox<'value> internal (queue: Queue<'value>) =
    let mutable dropped = 0L
    member internal _.Queue = queue
    member internal _.CountDrop() = Interlocked.Increment(&dropped) |> ignore // axial-allow-AXG009: Synchronous host dispatch can drop inbox messages on multiple threads; count each drop atomically.
    member _.Dropped = Interlocked.Read(&dropped) // axial-allow-AXG009: The synchronous Dropped property reads the 64-bit callback counter without tearing.

[<RequireQualifiedAccess>]
module Inbox =
    let create (capacity: int) (runtime: ElmishRuntime<'env>) : Inbox<'value> =
        if capacity <= 0 then invalidArg (nameof capacity) "Capacity must be positive"
        let queue = Queue.droppingNow capacity
        runtime.RegisterShutdown(fun () -> Queue.shutdownNow queue)
        Inbox<'value>(queue)

    let post value (inbox: Inbox<'value>) =
        match Queue.tryOffer value inbox.Queue with
        | QueueTryOfferResult.Accepted -> true
        | QueueTryOfferResult.Dropped | QueueTryOfferResult.Full ->
            inbox.CountDrop()
            false
        | _ -> false

    let offer value inbox : Cmd<'msg> = [ fun _ -> post value inbox |> ignore ]

    let dropped (inbox: Inbox<'value>) = inbox.Dropped

    let stream (inbox: Inbox<'value>) : FlowStream<'env, 'error, 'value> =
        FlowStream.fromDequeue inbox.Queue
