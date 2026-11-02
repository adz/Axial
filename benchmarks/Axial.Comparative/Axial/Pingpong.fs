// Mirrors FIO Effects/Pingpong.fs using unbounded FIFO queues.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Pingpong =
    let effect rounds : Flow<unit, exn, unit> = flow {
        let! start = Queue.unbounded ()
        let! ping = Queue.unbounded ()
        let! pong = Queue.unbounded ()
        let pinger = flow {
            let! _ = Dequeue.take start
            let mutable current = 1
            for _ in 1 .. rounds do
                do! Common.put current ping
                let! value = Dequeue.take pong
                current <- value + 1
        }
        let ponger = flow {
            do! Common.put 0 start
            for _ in 1 .. rounds do
                let! value = Dequeue.take ping
                do! Common.put (value + 1) pong
        }
        return! Flow.zipPar pinger ponger |> Flow.ignore
    }
