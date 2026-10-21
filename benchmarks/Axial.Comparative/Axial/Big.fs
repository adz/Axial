// Mirrors FIO Effects/Big.fs using separate unbounded ping and pong queues per actor.
namespace Axial.Comparative.Axial
open System
open Axial
[<RequireQualifiedAccess>]
module internal Big =
    type private Message = Ping of int * Queue<Message> | Pong of int
    let effect actorCount rounds : Flow<unit, exn, unit> = flow {
        let! pings = [1 .. actorCount] |> List.map (fun _ -> Queue.unbounded ()) |> Flow.sequence
        let! pongs = [1 .. actorCount] |> List.map (fun _ -> Queue.unbounded ()) |> Flow.sequence
        let pingArray, pongArray = List.toArray pings, List.toArray pongs
        let actor index =
            let peers = [ for i in 0 .. actorCount - 1 do if i <> index then yield pingArray[i] ]
            let rec send left = flow {
                for target in peers do do! Common.put (Ping(index * 10, pongArray[index])) target
                for _ in peers do
                    match! Dequeue.take pingArray[index] with
                    | Ping(value, reply) -> do! Common.put (Pong(value + 1)) reply
                    | _ -> return! Flow.fail (InvalidOperationException "Big: expected ping" :> exn)
                for _ in peers do
                    match! Dequeue.take pongArray[index] with
                    | Pong _ -> ()
                    | _ -> return! Flow.fail (InvalidOperationException "Big: expected pong" :> exn)
                if left > 1 then return! send (left - 1)
            }
            send rounds
        return! [ for i in 0 .. actorCount - 1 -> actor i ] |> Common.all
    }
