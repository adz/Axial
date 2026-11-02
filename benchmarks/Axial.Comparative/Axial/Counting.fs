// Mirrors FIO Effects/Counting.fs using an unbounded FIFO actor mailbox.
namespace Axial.Comparative.Axial
open System
open Axial
[<RequireQualifiedAccess>]
module internal Counting =
    type private Message = Increment | Retrieve of Queue<int>
    let value messageCount : Flow<unit, exn, int> = flow {
        let! mailbox = Queue.unbounded ()
        let! reply = Queue.unbounded ()
        let rec counter count = flow {
            match! Dequeue.take mailbox with
            | Increment -> return! counter (count + 1)
            | Retrieve target -> do! Common.put count target
        }
        let producer = flow {
            for _ in 1 .. messageCount do do! Common.put Increment mailbox
            do! Common.put (Retrieve reply) mailbox
            let! total = Dequeue.take reply
            if total <> messageCount then
                return! Flow.fail (InvalidOperationException $"Counting: expected {messageCount}, got {total}" :> exn)
            return total
        }
        let! _, total = Flow.zipPar (counter 0) producer
        return total
    }
    let effect messageCount = value messageCount |> Flow.ignore
