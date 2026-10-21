// Mirrors FIO Effects/Bang.fs. FIO Channel() and Axial Queue.unbounded are both unbounded FIFO mailboxes.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Bang =
    let effect actorCount roundCount : Flow<unit, exn, unit> = flow {
        let! mailbox = Queue.unbounded ()
        let receiver = flow { for _ in 1 .. actorCount * roundCount do let! _ = Dequeue.take mailbox in () }
        let sender message = flow { for _ in 1 .. roundCount do do! Common.put message mailbox }
        return! receiver :: [ for i in 1 .. actorCount -> sender i ] |> Common.all
    }
