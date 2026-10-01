/// Parallel combinators: bounded traversals, pooled resources, fail-fast zips, and races.
module Axial.TortureTest.Parallel

open System
open System.Threading
open Axial
open Axial.State
open Axial.TortureTest.Scenario

let private ms (value: float) = TimeSpan.FromMilliseconds value

// <snippet:torture-parallel>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let items = [ 1 .. round.Size 200 ]
    let bound = round.Next 6 + 1

    flow {
        // Bounded traversal: never more than the bound at once, results in input order.
        let active = ref 0
        let peak = ref 0

        let tracked value =
            flow {
                let now = increment active
                lock peak (fun () -> peak.Value <- max peak.Value now)
                do! Flow.sleep (ms (float (round.Next 2)))
                return value * 2
            }
            |> Flow.ensuring (Flow.delay (fun () -> decrement active |> ignore; Flow.ok ()))

        let! doubled = items |> Flow.traversePar (Parallelism.bounded bound) tracked
        let processed = ResizeArray<int>()
        do! items |> Flow.forEachPar (Parallelism.ofProcessors id) (fun value -> Flow.delay (fun () -> lock processed (fun () -> processed.Add value); Flow.ok ()))

        // Pooled resources: each is used by one item at a time, and every one acquired is released.
        let acquired = ref 0
        let released = ref 0
        let busy = Collections.Generic.HashSet<int>()
        let shared = ref false

        let pool =
            Resource.ofAsync
                (Flow.delay (fun () -> Flow.ok (increment acquired)))
                (fun _ _ -> async { increment released |> ignore })

        let useResource resource value =
            flow {
                lock busy (fun () -> if not (busy.Add resource) then shared.Value <- true)
                do! Flow.sleep (ms (float (round.Next 2)))
                lock busy (fun () -> busy.Remove resource |> ignore)
                return value
            }

        let! pooled = items |> Flow.traverseParUsing (Parallelism.bounded bound) pool useResource
        do! items |> Flow.forEachParUsing (Parallelism.bounded bound) pool (fun resource value -> useResource resource value |> Flow.ignore)

        // A failing item stops the traversal; every item that started still cleaned up.
        let started = ref 0
        let cleaned = ref 0
        let failAt = items.[round.Next items.Length]

        let! failed =
            items
            |> Flow.traversePar (Parallelism.bounded bound) (fun value ->
                flow {
                    increment started |> ignore
                    do! Flow.sleep (ms (float (round.Next 2)))
                    if value = failAt then return! Flow.fail value
                    return value
                }
                |> Flow.ensuring (Flow.delay (fun () -> increment cleaned |> ignore; Flow.ok ())))
            |> exitOf

        // zipPar fails fast and interrupts the other side; two failures are both reported.
        let otherInterrupted = ref false
        let! failFast = Flow.zipPar (Flow.fail "left") (Flow.never |> Flow.onInterrupt (Flow.delay (fun () -> otherInterrupted.Value <- true; Flow.ok ()))) |> exitOf
        let! bothFail = Flow.zipPar (Flow.sleep (ms 1.0) |> Flow.bind (fun () -> Flow.fail "a")) (Flow.fail "b") |> exitOf
        let! inOrder = [ for value in 1..20 -> Flow.sleep (ms (float (round.Next 3))) |> Flow.map (fun () -> value) ] |> Flow.sequencePar

        // Races: the winner's value comes back, and both sides always finish: the winner completes, and the loser
        // completes or is interrupted, running its exit handler either way.
        let races = round.Size 50
        let! finishes = [ for _ in 1 .. 2 * races -> Deferred.make<Axial.ClockEnvironment, Never, unit> () ] |> Flow.sequence

        let! winners =
            finishes
            |> List.chunkBySize 2
            |> Flow.traverse (fun pair ->
                let side (name: string) (finished: Deferred<Never, unit>) =
                    Flow.sleep (ms (float (round.Next 3)))
                    |> Flow.map (fun () -> name)
                    |> Flow.onExit (fun _ -> Deferred.succeed () finished |> Flow.ignore)

                Flow.race (side "left" pair[0]) (side "right" pair[1]))

        let! allSidesFinished =
            finishes
            |> Flow.traverse Deferred.await
            |> Flow.map (fun _ -> true)
            |> Flow.timeoutToOk (TimeSpan.FromSeconds 10.0) false

        let failures =
            match bothFail with
            | Exit.Failure cause -> Cause.failures cause |> List.sort
            | Exit.Success _ -> []

        return
            [ check "a bounded traversal never ran more than its bound, and kept input order" (peak.Value <= bound && doubled = List.map ((*) 2) items)
              check "forEachPar processed every item exactly once" (List.sort (List.ofSeq processed) = items)
              check "no pooled resource was used by two items at once, and every one was released" (not shared.Value && pooled = items && acquired.Value = released.Value && acquired.Value <= 2 * bound)
              check "a failing item failed the traversal, and every started item cleaned up" (failed = Exit.Failure(Cause.Fail failAt) && started.Value = cleaned.Value)
              check "zipPar failed fast and interrupted the other side" (failFast = Exit.Failure(Cause.Fail "left") && otherInterrupted.Value)
              check "zipPar reported the failures that happened" (not failures.IsEmpty && failures |> List.forall (fun failure -> failure = "a" || failure = "b"))
              check "sequencePar kept input order" (inOrder = [ 1..20 ])
              check "every race returned a side's value" (winners |> List.forall (fun winner -> winner = "left" || winner = "right"))
              check "both sides of every race finished and ran their exit handler" (allSidesFinished && winners.Length = races) ]
    }
// </snippet:torture-parallel>

let scenario : Scenario =
    { Name = "parallel"
      Title = "Parallel combinators: bounded traversals, pooled resources, fail-fast zips, and races"
      Run = run }
