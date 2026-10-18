// axial-allow-effect-file: blocking
namespace Axial.Elmish

open System
open System.Threading
open Axial
open global.Elmish

[<Sealed>]
type Inbox<'value> internal (queue: Queue<'value>) =
    let mutable dropped = 0L
    member internal _.Queue = queue
    member internal _.CountDrop() = Interlocked.Increment(&dropped) |> ignore
    member _.Dropped = Interlocked.Read(&dropped)

[<RequireQualifiedAccess>]
module Inbox =
    let create (capacity: int) (runtime: ElmishRuntime<'env>) : Inbox<'value> =
        if capacity <= 0 then invalidArg (nameof capacity) "Capacity must be positive"
        let queue =
            match App.run runtime.Environment (Queue.dropping capacity) |> Async.RunSynchronously with
            | Exit.Success queue -> queue
            | _ -> failwith "Could not create inbox"
        runtime.RegisterShutdown(fun () -> App.run runtime.Environment (Dequeue.shutdown queue) |> Async.RunSynchronously |> ignore)
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
