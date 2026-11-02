// Validation harnesses for values hidden by the upstream unit-returning benchmark effects.
namespace Axial.Comparative.Fio

open System
open FIO.DSL

[<RequireQualifiedAccess>]
module internal Validation =
    let rec private fibSequence n =
        if n < 2 then n else fibSequence (n - 1) + fibSequence (n - 2)

    let rec private fib n threshold =
        if n <= threshold then FIO.attempt (fun () -> fibSequence n) id
        else
            (fib (n - 1) threshold).Fork().FlatMap <| fun left ->
                (fib (n - 2) threshold).FlatMap <| fun right ->
                    left.Join().Map(fun leftValue -> leftValue + right)

    let fibonacci n threshold = fib n threshold

    type private CountingMessage = Increment | Retrieve of Channel<int>

    let counting messageCount = fio {
        let mailbox = Channel<CountingMessage>()
        let reply = Channel<int>()
        let rec counter count = fio {
            match! mailbox.Read() with
            | Increment -> return! counter (count + 1)
            | Retrieve target -> do! target.Write(count).Unit()
        }
        let producer = fio {
            for _ in 1 .. messageCount do do! mailbox.Write(Increment).Unit()
            do! mailbox.Write(Retrieve reply).Unit()
            return! reply.Read()
        }
        let! _, total = (counter 0).ZipPar(producer)
        return total
    }

    let trapezoidal workerCount pointCount = fio {
        let h = 1.0 / float pointCount
        let chunk = pointCount / workerCount
        let partial worker =
            let compute () =
                let lo = worker * chunk
                let hi = if worker = workerCount - 1 then pointCount else (worker + 1) * chunk
                let mutable sum = 0.0
                for i in lo .. (hi - 1) do
                    let x = (float i + 0.5) * h
                    sum <- sum + 4.0 / (1.0 + x * x)
                sum
            FIO.attempt compute id
        let! values = [ for worker in 0 .. workerCount - 1 -> partial worker ] |> FIO.collectAllPar
        return List.sum values * h
    }
