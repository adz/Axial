/// Software transactional memory: concurrent transfers, blocking withdrawals, and an auditor, with interruptions.
module Axial.TortureTest.Stm

open System
open Axial
open Axial.State
open Axial.TortureTest.Scenario

// <snippet:torture-stm>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let accounts = 6
    let opening = 100
    let transfers = round.Size 400
    let patient = 10

    flow {
        let! balances = [ for _ in 1..accounts -> TRef.make opening ] |> List.map STM.atomically |> Flow.sequence
        let! reserve = STM.atomically (TRef.make 0)
        let total () = balances |> List.fold (fun sum account -> stm { let! so = sum in let! b = TRef.get account in return so + b }) (stm { return 0 })

        // A transfer moves money only if the source can cover it; otherwise orElse records that it was skipped.
        let transfer source target amount : STM<bool> =
            STM.orElse
                (stm {
                    let! available = TRef.get balances[source]
                    if available < amount then return! STM.retry
                    do! balances[source] |> TRef.update (fun value -> value - amount)
                    do! balances[target] |> TRef.update ((+) amount)
                    return true
                 })
                (stm { return false })

        // A patient withdrawal waits, with STM.retry, until the reserve can cover it.
        let withdraw amount : STM<unit> =
            stm {
                let! available = TRef.get reserve
                if available < amount then return! STM.retry
                do! reserve |> TRef.set (available - amount)
            }

        let! waiting = [ for _ in 1..patient -> STM.atomically (withdraw 5) |> Flow.fork ] |> Flow.sequence

        // Transfer fibers, a random fifth of them interrupted, run while an auditor reads every balance at once.
        let! movers =
            [ for _ in 1..transfers ->
                  let source, target = round.Next accounts, round.Next accounts
                  STM.atomically (transfer source target (round.Next 30 + 1)) |> Flow.fork ]
            |> Flow.sequence

        let! audits =
            [ for _ in 1 .. round.Size 100 -> STM.atomically (total ()) ] |> Flow.sequence

        let! moved = movers |> Flow.traverse (fun fiber -> if round.Chance 20 then Fiber.interrupt fiber else Fiber.await fiber)

        // Fund the reserve in small deposits; every patient withdrawal must then complete.
        do! [ for _ in 1..patient -> STM.atomically (reserve |> TRef.update ((+) 5)) ] |> Flow.sequencePar |> Flow.ignore
        let! withdrawn = waiting |> Flow.traverse Fiber.await |> Flow.timeoutToOk (TimeSpan.FromSeconds 10.0) []

        let! final = STM.atomically (total ())
        let! finalBalances = balances |> List.map (TRef.get >> STM.atomically) |> Flow.sequence
        let! left = STM.atomically (TRef.get reserve)
        let completed = moved |> List.filter (function Exit.Success true -> true | _ -> false) |> List.length

        return
            [ check "every audit saw the whole amount, never a transfer half done" (audits |> List.forall ((=) (accounts * opening)))
              check "the total is unchanged after every transfer" (final = accounts * opening)
              check "no account ever went negative" (finalBalances |> List.forall (fun balance -> balance >= 0))
              check "every patient withdrawal completed once the reserve was funded" (withdrawn.Length = patient && withdrawn |> List.forall (function Exit.Success () -> true | _ -> false) && left = 0)
              check "some transfers went through" (completed > 0) ]
    }
// </snippet:torture-stm>

let scenario : Scenario =
    { Name = "stm"
      Title = "STM: concurrent transfers, blocking withdrawals, and atomic audits"
      Run = run }
