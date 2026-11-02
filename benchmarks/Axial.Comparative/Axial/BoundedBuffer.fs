// Mirrors FIO Effects/BoundedBuffer.fs. The actor owns the bounded store; communication queues remain unbounded as in FIO.
namespace Axial.Comparative.Axial
open System.Collections.Generic
open Axial
[<RequireQualifiedAccess>]
module internal BoundedBuffer =
    type private Message = Put of int * Axial.Queue<unit> | Get of Axial.Queue<int>
    let private distribute total parts = [ for i in 0 .. parts - 1 -> total / parts + (if i < total % parts then 1 else 0) ]
    let effect producers consumers capacity items : Flow<unit, exn, unit> = flow {
        let! mailbox = Queue.unbounded ()
        let total = producers * items
        let buffer = System.Collections.Generic.Queue<int>()
        let waitingP = System.Collections.Generic.Queue<int * Axial.Queue<unit>>()
        let waitingC = System.Collections.Generic.Queue<Axial.Queue<int>>()
        let rec actor delivered = flow {
            if delivered < total then
                match! Axial.Dequeue.take mailbox with
                | Put(item, ack) when waitingC.Count > 0 ->
                    let reply = waitingC.Dequeue()
                    do! Common.put item reply
                    do! Common.put () ack
                    return! actor (delivered + 1)
                | Put(item, ack) when buffer.Count < capacity ->
                    buffer.Enqueue item
                    do! Common.put () ack
                    return! actor delivered
                | Put(item, ack) ->
                    waitingP.Enqueue(item, ack)
                    return! actor delivered
                | Get reply when buffer.Count > 0 ->
                    let value = buffer.Dequeue()
                    do! Common.put value reply
                    if waitingP.Count > 0 then
                        let parked, ack = waitingP.Dequeue()
                        buffer.Enqueue parked
                        do! Common.put () ack
                    return! actor (delivered + 1)
                | Get reply ->
                    waitingC.Enqueue reply
                    return! actor delivered
        }
        let producer p = flow {
            let! ack = Queue.unbounded ()
            for i in 0 .. items - 1 do
                do! Common.put (Put(p * items + i + 1, ack)) mailbox
                let! _ = Axial.Dequeue.take ack
                ()
        }
        let consumer count = flow {
            let! reply = Queue.unbounded ()
            for _ in 1 .. count do
                do! Common.put (Get reply) mailbox
                let! _ = Axial.Dequeue.take reply
                ()
        }
        return! actor 0 :: ([ for p in 0 .. producers - 1 -> producer p ] @ [ for count in distribute total consumers -> consumer count ]) |> Common.all
    }
