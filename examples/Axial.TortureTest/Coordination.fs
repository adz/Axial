/// Semaphores, deferreds, and refs under contention and interruption.
module Axial.TortureTest.Coordination

open System
open Axial
open Axial.State
open Axial.TortureTest.Scenario

// <snippet:torture-semaphores>
let semaphores (round: Round) : Flow<unit, Never, Check list> =
    let permits = 3
    let workers = round.Size 60

    flow {
        let! semaphore = Semaphore.make permits
        let! active = Ref.make 0
        let! busiest = Ref.make 0
        let entered = ResizeArray<int>()

        // Each worker holds a permit while it counts itself in; ensuring counts it out however the worker ends.
        let worker id =
            flow {
                let! now = active |> Ref.updateAndGet ((+) 1)
                do! busiest |> Ref.update (max now)
                lock entered (fun () -> entered.Add id)
                do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 2)))
            }
            |> Flow.ensuring (active |> Ref.update (fun count -> count - 1))
            |> Semaphore.withPermit semaphore

        let! fibers = [ 1..workers ] |> Flow.traverse (worker >> Flow.fork)

        // Interrupt a random third of the workers, whether they are waiting for a permit or holding one.
        let! exits =
            fibers
            |> Flow.traverse (fun fiber -> if round.Chance 33 then Fiber.interrupt fiber else Fiber.await fiber)

        let! finalActive = Ref.get active
        let! mostAtOnce = Ref.get busiest

        // If an interrupted waiter had lost a permit, these three could not all hold one at the same time.
        let! barrier = Deferred.make<unit, Never, unit> ()
        let! arrived = Ref.make 0

        let holdTogether =
            flow {
                let! count = arrived |> Ref.updateAndGet ((+) 1)
                if count = permits then do! Deferred.succeed () barrier |> Flow.ignore
                do! Deferred.await barrier
            }
            |> Semaphore.withPermit semaphore

        let! allPermitsFree =
            List.replicate permits holdTogether
            |> Flow.sequencePar
            |> Flow.map (fun _ -> true)
            |> Flow.timeoutToOk (TimeSpan.FromSeconds 10.0) false

        let succeeded = exits |> List.filter (function Exit.Success _ -> true | _ -> false) |> List.length

        return
            [ check "no more workers than permits ever held one at once" (mostAtOnce <= permits && mostAtOnce >= 1)
              check "every worker that finished had entered" (succeeded <= entered.Count)
              check "every worker that entered was counted out, even if interrupted inside" (finalActive = 0)
              check "interrupted waiters returned their permits: all permits can be held together" allPermitsFree ]
    }
// </snippet:torture-semaphores>

// <snippet:torture-deferreds>
type private Attempt =
    | Succeed of int
    | Fail of int
    | Die of int
    | Complete of int
    | Interrupt

let deferreds (round: Round) : Flow<unit, Never, Check list> =
    let awaiters = round.Size 40

    flow {
        // Deferred.make's flow fails with the deferred's own error type; creating one never actually fails.
        let! deferred = Deferred.make<unit, int, int> () |> Flow.fold Flow.ok (fun _ -> Flow.die (InvalidOperationException "unreachable"))
        let! early = [ 1..awaiters ] |> Flow.traverse (fun _ -> deferred |> Deferred.await |> Flow.fork)

        // Several completers race, each with a different kind of completion; exactly one may win.
        let attempts =
            [ for id in 1..10 ->
                  match round.Next 5 with
                  | 0 -> Succeed id
                  | 1 -> Fail id
                  | 2 -> Die id
                  | 3 -> Complete id
                  | _ -> Interrupt ]

        let attempt kind =
            match kind with
            | Succeed id -> deferred |> Deferred.succeed id
            | Fail id -> deferred |> Deferred.fail id
            | Die id -> deferred |> Deferred.die (InvalidOperationException(string id))
            | Complete id -> deferred |> Deferred.complete (Exit.Success(id * 100))
            | Interrupt -> deferred |> Deferred.interrupt

        // Interrupt a random half of the early awaiters while the completers race.
        let interrupter =
            early |> Flow.traverse (fun fiber -> if round.Chance 50 then Fiber.interrupt fiber else Fiber.await fiber)

        let! won, earlyExits = Flow.zipPar (attempts |> Flow.traversePar (Parallelism.bounded 10) attempt) interrupter
        let! late = deferred |> Deferred.await |> exitOf

        let winners = List.zip attempts won |> List.filter snd |> List.map fst

        let expected: Exit<int, int> option =
            match winners with
            | [ Succeed id ] -> Some(Exit.Success id)
            | [ Fail id ] -> Some(Exit.Failure(Cause.Fail id))
            | [ Complete id ] -> Some(Exit.Success(id * 100))
            | [ Interrupt ] -> Some(Exit.Failure Cause.Interrupt)
            | _ -> None

        let matches (exit: Exit<int, int>) =
            match winners, exit with
            | [ Die id ], Exit.Failure(Cause.Die error) -> error.Message = string id
            | _ -> Some exit = expected

        let observed = earlyExits |> List.filter (isInterrupted >> not)

        return
            [ check "exactly one completion won" (winners.Length = 1)
              check "every awaiter that was not interrupted saw the winning outcome" (observed |> List.forall matches)
              check "an awaiter arriving afterwards sees the same outcome" (matches late) ]
    }
// </snippet:torture-deferreds>

// <snippet:torture-refs>
let refs (round: Round) : Flow<unit, Never, Check list> =
    let fibers = 8
    let perFiber = round.Size 250

    flow {
        let! counter = Ref.make 0

        // Every operation adds one. Those that report the value they replaced must each see a different one.
        let increments =
            flow {
                let observed = ResizeArray<int>()

                for _ in 1..perFiber do
                    match round.Next 4 with
                    | 0 -> do! counter |> Ref.update ((+) 1)
                    | 1 ->
                        let! before = counter |> Ref.modify (fun value -> value, value + 1)
                        observed.Add before
                    | 2 ->
                        let! before = counter |> Ref.getAndUpdate ((+) 1)
                        observed.Add before
                    | _ ->
                        let! after = counter |> Ref.updateAndGet ((+) 1)
                        observed.Add(after - 1)

                return List.ofSeq observed
            }

        let! observed = List.replicate fibers increments |> Flow.sequencePar |> Flow.map List.concat
        let! total = Ref.get counter

        // Tokens passed around with getAndSet: every token is handed on exactly once.
        let! slot = Ref.make 0

        let! returned =
            [ for fiber in 1..fibers -> [ for index in 1..perFiber -> slot |> Ref.getAndSet (fiber * 100000 + index) ] |> Flow.sequence ]
            |> Flow.sequencePar
            |> Flow.map List.concat

        let! last = Ref.get slot
        do! slot |> Ref.set -1
        let! reset = Ref.get slot
        let placed = [ for fiber in 1..fibers do for index in 1..perFiber -> fiber * 100000 + index ]

        // A SubscriptionRef updated concurrently: a stream that started first sees every value in turn.
        // The stream subscribes when it starts; waiting for its first value means it is subscribed before any update.
        let! level = SubscriptionRef.make 0
        let! subscribed = Deferred.make<unit, Never, unit> ()

        let! history =
            level
            |> SubscriptionRef.changes QueueStrategy.Unbounded
            |> FlowStream.tapFlow (fun _ -> Deferred.succeed () subscribed |> Flow.ignore)
            |> FlowStream.take (fibers * perFiber + 1)
            |> FlowStream.runCollect
            |> Flow.fork

        do! Deferred.await subscribed

        do!
            List.replicate fibers (flow {
                for index in 1..perFiber do
                    if index % 2 = 0 then do! level |> SubscriptionRef.update ((+) 1)
                    else do! level |> SubscriptionRef.modify (fun value -> (), value + 1)
            })
            |> Flow.sequencePar
            |> Flow.ignore

        let! finalLevel = SubscriptionRef.get level
        do! level |> SubscriptionRef.set 0
        let! seen = Fiber.join history

        return
            [ check "no increment was lost" (total = fibers * perFiber)
              check "every increment that reported a value saw a different one" (List.length (List.distinct observed) = observed.Length)
              check "getAndSet handed every token on exactly once" (List.sort (last :: returned) = List.sort (0 :: placed) && reset = -1)
              check "a SubscriptionRef ends at the total" (finalLevel = fibers * perFiber)
              check "its change stream saw every value, in order" (seen = [ 0 .. fibers * perFiber ]) ]
    }
// </snippet:torture-refs>

let semaphoreScenario : Scenario =
    { Name = "semaphores"
      Title = "Semaphore: permits under contention, with waiters and holders interrupted"
      Run = semaphores }

let deferredScenario : Scenario =
    { Name = "deferreds"
      Title = "Deferred: racing completions, interrupted awaiters, late awaiters"
      Run = deferreds }

let refScenario : Scenario =
    { Name = "refs"
      Title = "Ref and SubscriptionRef: concurrent updates, no lost or duplicated observation"
      Run = refs }
