// Mirrors FIO Effects/Chameneos.fs using unbounded request and per-creature reply queues.
namespace Axial.Comparative.Axial
open Axial
[<RequireQualifiedAccess>]
module internal Chameneos =
    type private Color = Red | Yellow | Blue
    type private Response = Paired of Color | Stop
    type private Request = { Color: Color; Reply: Queue<Response> }
    let private complement = function Red,Red -> Red | Yellow,Yellow -> Yellow | Blue,Blue -> Blue | Red,Yellow | Yellow,Red -> Blue | Red,Blue | Blue,Red -> Yellow | _ -> Red
    let effect creatureCount meetings : Flow<unit, exn, unit> = flow {
        let! mall = Queue.unbounded ()
        let rec mallLoop left waiter stopped = flow {
            if stopped < creatureCount then
                let! request = Dequeue.take mall
                if left <= 0 then
                    do! Common.put Stop request.Reply
                    return! mallLoop left waiter (stopped + 1)
                else match waiter with
                     | None -> return! mallLoop left (Some request) stopped
                     | Some first ->
                         do! Common.put (Paired request.Color) first.Reply
                         do! Common.put (Paired first.Color) request.Reply
                         return! mallLoop (left - 1) None stopped
        }
        let creature color = flow {
            let! reply = Queue.unbounded ()
            let rec loop current = flow {
                do! Common.put { Color = current; Reply = reply } mall
                match! Dequeue.take reply with Stop -> () | Paired other -> return! loop (complement(current, other))
            }
            return! loop color
        }
        let colors = [|Red; Yellow; Blue|]
        return! mallLoop meetings None 0 :: [for i in 0 .. creatureCount - 1 -> creature colors[i % 3]] |> Common.all
    }
