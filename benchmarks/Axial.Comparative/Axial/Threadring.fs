// Mirrors FIO Effects/Threadring.fs using one unbounded FIFO queue per actor.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Threadring =
    let effect actorCount rounds : Flow<unit, exn, unit> = flow {
        let! queues = [1 .. actorCount] |> List.map (fun _ -> Queue.unbounded ()) |> Flow.sequence
        let array = List.toArray queues
        let actor index = flow {
            let receive = array[if index = 0 then actorCount - 1 else index - 1]
            for round in 1 .. rounds do
                let! value = Dequeue.take receive
                if not (index = actorCount - 1 && round = rounds) then do! Common.put (value + 1) array[index]
        }
        do! Common.put 0 array[actorCount - 1]
        return! [ for i in 0 .. actorCount - 1 -> actor i ] |> Common.all
    }
