// Mirrors FIO Effects/Philosophers.fs: the primary Axial port uses the same single-arbitrator actor design.
namespace Axial.Comparative.Axial
open System.Collections.Generic
open Axial
[<RequireQualifiedAccess>]
module internal Philosophers =
    type private Message = Hungry of int * Queue<unit> | Done of int
    let effect count rounds : Flow<unit, exn, unit> = flow {
        let! mailbox = Queue.unbounded ()
        let forks = Array.create count true
        let pending = System.Collections.Generic.Queue<int * Axial.Queue<unit>>()
        let canEat id = forks[id] && forks[(id + 1) % count]
        let take id = forks[id] <- false; forks[(id + 1) % count] <- false
        let release id = forks[id] <- true; forks[(id + 1) % count] <- true
        let grantPending () = flow {
            let scan = pending.Count
            for _ in 1 .. scan do
                let id, reply = pending.Dequeue()
                if canEat id then
                    take id
                    do! Common.put () reply
                else
                    pending.Enqueue(id, reply)
        }
        let rec arbiter doneCount = flow {
            if doneCount < count * rounds then
                match! Dequeue.take mailbox with
                | Hungry(id, reply) when canEat id ->
                    take id
                    do! Common.put () reply
                    return! arbiter doneCount
                | Hungry(id, reply) ->
                    pending.Enqueue(id, reply)
                    return! arbiter doneCount
                | Done id ->
                    release id
                    do! grantPending ()
                    return! arbiter (doneCount + 1)
        }
        let philosopher id = flow {
            let! reply = Queue.unbounded ()
            for _ in 1 .. rounds do
                do! Common.put (Hungry(id, reply)) mailbox
                let! _ = Dequeue.take reply
                do! Common.put (Done id) mailbox
        }
        return! arbiter 0 :: [for i in 0 .. count - 1 -> philosopher i] |> Common.all
    }
