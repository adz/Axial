/// Schedules driving retry, repeat, and supervise, run concurrently; timeouts racing their operations.
module Axial.TortureTest.Schedules

open System
open System.Threading
open Axial
open Axial.TortureTest.Scenario

let private ms (value: float) = TimeSpan.FromMilliseconds value

// <snippet:torture-schedules>
/// Retries a flow that fails every time and returns how many times it ran.
let private runsUntilStopped (schedule: Schedule<Axial.ClockEnvironment, string, 'output>) : Flow<Axial.ClockEnvironment, Never, int> =
    flow {
        let runs = ref 0
        let! _ = Flow.delay (fun () -> increment runs |> ignore; Flow.fail "always") |> Flow.retry schedule |> exitOf
        return runs.Value
    }

let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let cases = round.Size 40

    flow {
        // Each case picks random counts and checks the documented number of runs; the cases run concurrently.
        let case _ =
            flow {
                let a, b = round.Next 6, round.Next 6
                let! plain = runsUntilStopped (Schedule.recurs a)
                let! atMost = runsUntilStopped (Schedule.spaced TimeSpan.Zero |> Schedule.recursAtMost a)
                let! either = runsUntilStopped (Schedule.recurs a |> Schedule.union (Schedule.recurs b))
                let! both = runsUntilStopped (Schedule.recurs a |> Schedule.intersect (Schedule.exponential (ms 0.01) |> Schedule.jitteredWith (fun () -> 0.5)) |> Schedule.intersect (Schedule.recurs b))
                let! phased = runsUntilStopped (Schedule.recurs a |> Schedule.andThen (Schedule.recurs b))
                let! whileSmall = runsUntilStopped (Schedule.recurs 100 |> Schedule.map ((*) 2) |> Schedule.whileOutput (fun output -> output < 2 * a))
                let! untilLarge = runsUntilStopped (Schedule.recurs 100 |> Schedule.untilOutput (fun output -> output >= b))
                let! healthy = runsUntilStopped (Schedule.recurs a |> Schedule.resetAfter (TimeSpan.FromHours 1.0))
                let! ticks = Schedule.recurs a |> FlowStream.fromSchedule |> FlowStream.runCollect

                return
                    plain = a + 1
                    && atMost = a + 1
                    && either = max a b + 1
                    && both = min a b + 1
                    && phased = a + b + 1
                    && whileSmall = a + 1
                    && untilLarge = b + 1
                    && healthy = a + 1
                    && ticks.Length = a
            }

        let! counted = List.init cases case |> Flow.sequencePar

        // Retrying only some errors: transient failures are retried, the first fatal one stops the retry.
        let failing (errors: string list) =
            let remaining = ref errors

            Flow.delay (fun () ->
                match lock remaining (fun () -> match remaining.Value with head :: tail -> remaining.Value <- tail; Some head | [] -> None) with
                | Some error -> Flow.fail error
                | None -> Flow.ok "done")

        let! selective = failing [ "transient"; "transient"; "fatal"; "transient" ] |> Flow.retry (Schedule.recurs 10 |> Schedule.whileInput ((=) "transient")) |> exitOf
        let! stopping = failing [ "transient"; "fatal" ] |> Flow.retry (Schedule.recurs 10 |> Schedule.untilInput ((=) "fatal")) |> exitOf
        let! described = failing [ "a"; "b"; "c" ] |> Flow.retry (Retry.schedule { Retry.defaults with Retries = 3; Backoff = Backoff.Exponential(ms 0.01, ms 0.1) }) |> exitOf
        let! fixedBackoff = failing [ "a"; "b" ] |> Flow.retry (Retry.schedule { Retry.defaults with Backoff = Backoff.Fixed TimeSpan.Zero; Retries = 2 }) |> exitOf
        let! filtered = failing [ "skip" ] |> Flow.retry (Retry.schedule { Retry.defaults with Backoff = Backoff.NoDelay; When = (<>) "skip" }) |> exitOf

        // Repeat on a fixed rate: runs shorter than the period start on the start + n * period grid and never before
        // their tick. A run that overruns under load shifts the grid, so checking stops at the first overrun.
        let runs = ResizeArray<TimeSpan * TimeSpan>()
        let elapsed = stopwatch ()
        let period = 8.0

        let! _ =
            flow {
                let started = elapsed ()
                do! Flow.sleep (ms (float (round.Next 3)))
                runs.Add(started, elapsed ())
            }
            |> Flow.repeat (Schedule.fixedRate (ms period) |> Schedule.intersect (Schedule.recurs 5))

        let tolerance = period / 10.0
        let origin = (fst runs[0]).TotalMilliseconds

        let onGrid =
            runs
            |> Seq.pairwise
            |> Seq.indexed
            |> Seq.takeWhile (fun (index, ((_, previousEnd), _)) -> previousEnd.TotalMilliseconds - origin < float (index + 1) * period)
            |> Seq.forall (fun (index, (_, (start, _))) -> start.TotalMilliseconds - origin >= float (index + 1) * period - tolerance)

        // Time-bounded schedules always stop, and report elapsed time that never goes backwards.
        let elapsedSeen = ResizeArray<TimeSpan>()
        let! budgeted = runsUntilStopped (Schedule.spaced (ms 1.0) |> Schedule.upTo (ms 15.0))

        let! _ =
            Flow.ok ()
            |> Flow.repeat (Schedule.elapsed |> Schedule.map (fun elapsed -> elapsedSeen.Add elapsed; elapsed) |> Schedule.whileOutput (fun elapsed -> elapsed < ms 10.0))

        let elapsedMonotonic = elapsedSeen |> Seq.pairwise |> Seq.forall (fun (a, b) -> b >= a)

        return
            [ check "every combined schedule ran exactly as many times as its rules say" (counted |> List.forall id)
              check "whileInput retried the transient errors and stopped at the fatal one" (selective = Exit.Failure(Cause.Fail "fatal"))
              check "untilInput stopped at the fatal error" (stopping = Exit.Failure(Cause.Fail "fatal"))
              check "Retry records retried as configured" (described = Exit.Success "done" && fixedBackoff = Exit.Success "done" && filtered = Exit.Failure(Cause.Fail "skip"))
              check "a fixed-rate repeat never started a run before its tick" (runs.Count = 6 && onGrid)
              check "time budgets stopped the schedule, and elapsed time never went backwards" (budgeted >= 1 && elapsedMonotonic) ]
    }

/// supervise restarts defects only; timeouts stop their operation before returning.
let supervision (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let workers = round.Size 30

    flow {
        // A worker dies with a defect a random number of times, then succeeds. It is restarted while the schedule allows.
        let crashing crashes limit =
            flow {
                let runs = ref 0

                let! exit =
                    Flow.delay (fun () ->
                        let run = increment runs
                        if run <= crashes then Flow.die (InvalidOperationException "crash") else Flow.ok run)
                    |> Flow.supervise (Schedule.recurs limit)
                    |> exitOf

                return crashes, limit, runs.Value, exit
            }

        let! supervised = [ for _ in 1..workers -> crashing (round.Next 5) (round.Next 5) ] |> Flow.sequencePar

        let restartedAsAllowed =
            supervised
            |> List.forall (fun (crashes, limit, runs, exit) ->
                if crashes <= limit then runs = crashes + 1 && exit = Exit.Success(crashes + 1)
                else runs = limit + 1 && (match exit with Exit.Failure(Cause.Die _) -> true | _ -> false))

        // Typed failures and interruptions are never restarted.
        let typedRuns = ref 0
        let! typed = Flow.delay (fun () -> increment typedRuns |> ignore; Flow.fail "typed") |> Flow.supervise (Schedule.recurs 5) |> exitOf
        let hungRuns = ref 0
        let! hung = Flow.delay (fun () -> increment hungRuns |> ignore; Flow.never) |> Flow.supervise (Schedule.recurs 5) |> Flow.fork
        do! Flow.sleep (ms 2.0)
        let! hungExit = Fiber.interrupt hung

        // Timeouts race operations of random length. Whichever wins, the operation has settled before the timeout
        // returns, so its cleanup has always run.
        let timed index =
            flow {
                let settled = ref false

                let operation : Flow<Axial.ClockEnvironment, string, int> =
                    Flow.sleep (ms (float (round.Next 5))) |> Flow.map (fun () -> index) |> Flow.onExit (fun _ -> Flow.delay (fun () -> settled.Value <- true; Flow.ok ()))

                let! exit =
                    match round.Next 4 with
                    | 0 -> operation |> Flow.timeout (ms 2.0) "timed out" |> exitOf
                    | 1 -> operation |> Flow.timeoutToError (ms 2.0) "timed out" |> exitOf
                    | 2 -> operation |> Flow.timeoutToOk (ms 2.0) -1 |> exitOf
                    | _ -> operation |> Flow.timeoutWith (ms 2.0) (fun () -> Flow.ok -2) |> exitOf

                let valid =
                    match exit with
                    | Exit.Success value -> value = index || value = -1 || value = -2
                    | Exit.Failure(Cause.Fail "timed out") -> true
                    | _ -> false

                return valid && settled.Value
            }

        let! timings = [ 1 .. round.Size 60 ] |> List.map timed |> Flow.sequencePar

        return
            [ check "supervise restarted every defect while the schedule allowed, and no more" restartedAsAllowed
              check "a typed failure was not restarted" (typed = Exit.Failure(Cause.Fail "typed") && typedRuns.Value = 1)
              check "an interruption was not restarted" (isInterrupted hungExit && hungRuns.Value = 1)
              check "every timeout returned the value or its fallback, after its operation had settled" (timings |> List.forall id) ]
    }
// </snippet:torture-schedules>

let scenario : Scenario =
    { Name = "schedules"
      Title = "Schedules, retry, repeat, supervise, and timeouts"
      Run = fun round -> Flow.map2 (@) (run round) (supervision round) }
