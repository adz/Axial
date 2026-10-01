/// Single-flight caches and memoized flows under concurrent callers, interruptions, failures, and invalidation.
module Axial.TortureTest.Caches

open System
open Axial
open Axial.TortureTest.Scenario

// <snippet:torture-caches>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let keys = 8
    let callersPerKey = round.Size 25

    flow {
        let gate = obj ()
        let runs = Array.zeroCreate<int> keys
        let succeeded = Collections.Generic.HashSet<int * int>()
        let invalidations = Array.zeroCreate<int> keys

        // Odd keys fail on their first run: a failure must not be cached, so a later caller runs the lookup again.
        let lookup key : Flow<Axial.ClockEnvironment, string, int * int> =
            flow {
                let attempt = lock gate (fun () -> runs[key] <- runs[key] + 1; runs[key])
                do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 3)))

                if key % 2 = 1 && attempt = 1 then
                    return! Flow.fail "boom"

                lock gate (fun () -> succeeded.Add((key, attempt)) |> ignore)
                return key, attempt
            }

        let! cache = Cache.make lookup

        // Many callers per key; a random fifth are interrupted as soon as they start waiting.
        let! callers =
            [ for key in 0 .. keys - 1 do for _ in 1..callersPerKey -> key ]
            |> Flow.traverse (fun key -> cache |> Cache.get key |> Flow.fork |> Flow.map (fun fiber -> key, fiber))

        let invalidator =
            flow {
                for _ in 1 .. round.Size 10 do
                    let key = round.Next keys
                    lock gate (fun () -> invalidations[key] <- invalidations[key] + 1)
                    do! cache |> Cache.invalidate key
                    do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
            }

        let! exits, () =
            Flow.zipPar
                (callers |> Flow.traverse (fun (key, fiber) -> (if round.Chance 20 then Fiber.interrupt fiber else Fiber.await fiber) |> Flow.map (fun exit -> key, exit)))
                invalidator

        // With the invalidator done, each key is looked up at most once more, then served from the cache.
        let! settledValues = [ 0 .. keys - 1 ] |> Flow.traverse (fun key -> cache |> Cache.get key |> exitOf |> Flow.bind (fun first -> match first with Exit.Success _ -> Flow.ok first | _ -> cache |> Cache.get key |> exitOf))
        let runsBefore = lock gate (fun () -> Array.copy runs)
        let! cachedAgain = [ 0 .. keys - 1 ] |> Flow.traverse (fun key -> cache |> Cache.get key |> exitOf)
        let runsAfter = lock gate (fun () -> Array.copy runs)
        let! countBefore = Cache.count cache
        do! Cache.invalidateAll cache
        let! countAfter = Cache.count cache

        let succeededRuns key = lock gate (fun () -> succeeded |> Seq.filter (fst >> (=) key) |> Seq.length)

        return
            [ check
                  "every value a caller received came from a lookup that succeeded"
                  (exits |> List.forall (fun (_, exit) -> match exit with Exit.Success value -> lock gate (fun () -> succeeded.Contains value) | _ -> true))
              check
                  "a caller that was not interrupted got a value or the lookup's own failure"
                  (exits |> List.forall (fun (_, exit) -> match exit with Exit.Success _ -> true | Exit.Failure(Cause.Fail "boom") -> true | exit -> isInterrupted exit))
              check "a key succeeded at most once per invalidation" ([ 0 .. keys - 1 ] |> List.forall (fun key -> succeededRuns key <= 1 + invalidations[key]))
              check "a failed lookup was not cached: every key ends with a value" (settledValues |> List.forall (function Exit.Success _ -> true | _ -> false))
              check "a cached value is served without running the lookup again" (runsBefore = runsAfter && cachedAgain = settledValues)
              check "invalidateAll empties the cache" (countBefore = keys && countAfter = 0) ]
    }

/// A memoized flow shared by concurrent callers: one success runs once, a failure is retried by the next caller.
let memoized (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let callers = round.Size 50

    flow {
        let runs = ref 0

        let expensive : Flow<Axial.ClockEnvironment, string, int> =
            flow {
                let attempt = increment runs
                do! Flow.sleep (TimeSpan.FromMilliseconds 2.0)
                if attempt = 1 then return! Flow.fail "first attempt fails"
                return attempt
            }

        let! shared = Flow.memoize expensive
        let! first = shared |> exitOf
        let! values = List.replicate callers (shared |> exitOf) |> Flow.sequencePar
        let succeededWith = values |> List.choose (function Exit.Success value -> Some value | _ -> None) |> List.distinct

        return
            [ check "the first failure was not remembered" (first = Exit.Failure(Cause.Fail "first attempt fails"))
              check "concurrent callers shared one successful run" (succeededWith = [ 2 ] && values |> List.forall ((=) (Exit.Success 2)) && runs.Value = 2) ]
    }
// </snippet:torture-caches>

let scenario : Scenario =
    { Name = "caches"
      Title = "Cache and memoize: single-flight lookups with interrupted callers, failures, and invalidation"
      Run = fun round -> Flow.map2 (@) (run round) (memoized round) }
