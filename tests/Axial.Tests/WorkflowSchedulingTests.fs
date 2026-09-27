namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowSchedulingTests =
    [<Fact>]
    let ``Scheduling: retry failing flow`` () =
        let mutable attempts = 0
        let workflow : Flow<unit, string, string> =
            flow {
                attempts <- attempts + 1
                if attempts < 3 then
                    return! Flow.fail "try again"
                else
                    return "success"
            }

        let retried : Flow<unit, string, string> =
            workflow |> Flow.retry (Schedule.recurs 5)

        let result = Flow.runSync () retried
        
        test <@ result = Exit.Success "success" @>
        test <@ attempts = 3 @>

    [<Fact>]
    let ``Scheduling: repeat successful flow`` () =
        let mutable count = 0
        let workflow : Flow<unit, unit, int> =
            flow {
                count <- count + 1
                return count
            }

        let repeated : Flow<unit, unit, int> =
            workflow |> Flow.repeat (Schedule.recurs 3)

        let result = Flow.runSync () repeated
        
        test <@ result = Exit.Success 4 @>
        test <@ count = 4 @>

    [<Fact>]
    let ``Scheduling: recurs n means n additional attempts, not n total`` () =
        let mutable retryAttempts = 0
        let retryWorkflow : Flow<unit, string, string> =
            flow {
                retryAttempts <- retryAttempts + 1
                return! Flow.fail "always fails"
            }

        let retryResult = retryWorkflow |> Flow.retry (Schedule.recurs 3) |> Flow.runSync ()

        // recurs 3 permits the schedule to fire at attempt 0, 1, 2 (three retries), after the
        // one initial try that retry/repeat always perform for free: 1 + 3 = 4 executions.
        test <@ retryAttempts = 4 @>
        test <@ retryResult = Exit.Failure(Cause.Fail "always fails") @>

        let mutable repeatCount = 0
        let repeatWorkflow : Flow<unit, unit, int> =
            flow {
                repeatCount <- repeatCount + 1
                return repeatCount
            }

        let repeatResult = repeatWorkflow |> Flow.repeat (Schedule.recurs 3) |> Flow.runSync ()

        test <@ repeatCount = 4 @>
        test <@ repeatResult = Exit.Success 4 @>

        let mutable zeroRecursAttempts = 0
        let zeroRecursWorkflow : Flow<unit, string, string> =
            flow {
                zeroRecursAttempts <- zeroRecursAttempts + 1
                return! Flow.fail "fails"
            }

        zeroRecursWorkflow |> Flow.retry (Schedule.recurs 0) |> Flow.runSync () |> ignore

        // recurs 0 permits no retries at all: only the one initial, unretried try runs.
        test <@ zeroRecursAttempts = 1 @>

    [<Fact>]
    let ``Scheduling: a Schedule value is stateless and reusable across independent runs`` () =
        let schedule = Schedule.recurs 2

        let run () =
            let mutable attempts = 0
            let workflow : Flow<unit, string, string> =
                flow {
                    attempts <- attempts + 1
                    return! Flow.fail "always fails"
                }

            workflow |> Flow.retry schedule |> Flow.runSync () |> ignore
            attempts

        // Reusing the same Schedule value across separate retry runs must not leak attempt
        // state between them: each run starts counting from attempt 0 again.
        test <@ run () = 3 @>
        test <@ run () = 3 @>
        test <@ run () = 3 @>

    [<Fact>]
    let ``Scheduling: retry does not retry defects or interruptions`` () =
        let mutable defectAttempts = 0
        let defectResult =
            flow {
                defectAttempts <- defectAttempts + 1
                return! Flow.die (InvalidOperationException "boom")
            }
            |> Flow.retry (Schedule.recurs 5)
            |> Flow.runSync ()

        test <@ defectAttempts = 1 @>
        match defectResult with
        | Exit.Failure(Cause.Die error) -> test <@ error.Message = "boom" @>
        | other -> failwithf "Expected defect, got %A" other

        let mutable interruptAttempts = 0
        let interruptResult : Exit<string, string> =
            flow {
                interruptAttempts <- interruptAttempts + 1
                return! Flow.ofExit (Exit.Failure Cause.Interrupt)
            }
            |> Flow.retry (Schedule.recurs 5)
            |> Flow.runSync ()

        test <@ interruptAttempts = 1 @>
        test <@ interruptResult = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Scheduling: repeat surfaces schedule evaluation failure as a defect`` () =
        let failingSchedule : Schedule<unit, int, int> =
            Schedule(fun _ _ -> Flow.fail ())

        let result : Exit<int, string> =
            Flow.ok 1
            |> Flow.repeat failingSchedule
            |> Flow.runSync ()

        match result with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "Schedule evaluation failed." @>
        | other -> failwithf "Expected a defect, got %A" other

    [<Fact>]
    let ``Scheduling: interrupting the delay surfaces as interruption, not a typed error`` () =
        use cts = new CancellationTokenSource()
        cts.CancelAfter(TimeSpan.FromMilliseconds 20.0)

        let repeatResult : Exit<int, string> =
            Flow.ok 1
            |> Flow.repeat (Schedule.spaced (TimeSpan.FromSeconds 30.0))
            |> Flow.runSyncWithToken () cts.Token

        test <@ repeatResult = Exit.Failure Cause.Interrupt @>

        use retryCts = new CancellationTokenSource()
        retryCts.CancelAfter(TimeSpan.FromMilliseconds 20.0)

        let retryResult : Exit<int, string> =
            Flow.fail "transient"
            |> Flow.retry (Schedule.spaced (TimeSpan.FromSeconds 30.0))
            |> Flow.runSyncWithToken () retryCts.Token

        test <@ retryResult = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Scheduling: exponential caps instead of overflowing and rejects negative delays`` () =
        let (Schedule op) = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)

        let delayAt attempt =
            match Flow.runSync () (op 0 (ScheduleContext.ofAttempt attempt)) with
            | Exit.Success (_, delay) -> delay
            | other -> failwithf "Expected a delay decision, got %A" other

        test <@ delayAt 1 = TimeSpan.FromMilliseconds 200.0 @>
        test <@ delayAt 100 = TimeSpan.MaxValue @>
        test <@ delayAt 100 >= TimeSpan.Zero @>

        raises<ArgumentException> <@ Schedule.exponential (TimeSpan.FromSeconds -1.0) @>
        raises<ArgumentException> <@ Schedule.spaced (TimeSpan.FromSeconds -1.0) @>

    [<Fact>]
    let ``Scheduling: jitteredWith applies a deterministic sample to every delay`` () =
        let (Schedule op) =
            Schedule.spaced (TimeSpan.FromSeconds 1.0)
            |> Schedule.jitteredWith (fun () -> 0.25)

        let delayAt attempt =
            match Flow.runSync () (op 0 (ScheduleContext.ofAttempt attempt)) with
            | Exit.Success (_, delay) -> delay
            | other -> failwithf "Expected a delay decision, got %A" other

        test <@ delayAt 0 = TimeSpan.FromMilliseconds 750.0 @>
        test <@ delayAt 5 = TimeSpan.FromMilliseconds 750.0 @>

        let (Schedule cappedOp) =
            Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
            |> Schedule.jitteredWith (fun () -> 0.999)

        let cappedDelay =
            match Flow.runSync () (cappedOp TimeSpan.Zero (ScheduleContext.ofAttempt 100)) with
            | Exit.Success (_, delay) -> delay
            | other -> failwithf "Expected a delay decision, got %A" other

        test <@ cappedDelay = TimeSpan.MaxValue @>

    [<Fact>]
    let ``Scheduling: jitteredWith clamps out-of-contract samples instead of throwing`` () =
        // The documented contract is sample() in [0.0, 1.0), but jitteredWith never validates it:
        // out-of-range samples are clamped, not rejected. Pin that down so it stays a deliberate
        // choice, not an accident.
        let delayWithSample baseDelay sample =
            let (Schedule op) = Schedule.spaced baseDelay |> Schedule.jitteredWith sample

            match Flow.runSync () (op 0 (ScheduleContext.ofAttempt 0)) with
            | Exit.Success(_, delay) -> delay
            | other -> failwithf "Expected a delay decision, got %A" other

        test <@ delayWithSample (TimeSpan.FromSeconds 1.0) (fun () -> -1.0) = TimeSpan.Zero @>
        test <@ delayWithSample TimeSpan.MaxValue (fun () -> 5.0) = TimeSpan.MaxValue @>

    [<Fact>]
    let ``Flow runtime helpers cover timeout and retry`` () =
        let timeoutResult =
            Flow.sleep (TimeSpan.FromMilliseconds 20.0)
            |> Flow.timeout (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> Flow.runSync ()

        let retryRuns = ref 0

        let retryWorkflow =
            let policy =
                Retry.schedule
                    { Retry.defaults with
                        Retries = 2
                        Backoff = Backoff.NoDelay
                        When = fun error -> error = "transient" }

            Flow.delay(fun () ->
                retryRuns.Value <- retryRuns.Value + 1

                if retryRuns.Value < 2 then
                    Flow.fail "transient"
                else
                    Flow.succeed 42)
            |> Flow.retry policy

        let retryResult =
            retryWorkflow
            |> Flow.runSync ()

        test <@ timeoutResult = Exit.Failure (Cause.Fail "timed out") @>
        test <@ retryResult = Exit.Success 42 @>
        test <@ retryRuns.Value = 2 @>

    [<Fact>]
    let ``Flow retry does not retry defects or interruptions`` () =
        let retryRuns = ref 0

        let policy = Schedule.recurs 2

        let defectResult =
            Flow.delay(fun () ->
                retryRuns.Value <- retryRuns.Value + 1
                Flow.die (InvalidOperationException "boom"))
            |> Flow.retry policy
            |> Flow.runSync ()

        test <@ retryRuns.Value = 1 @>
        match defectResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "boom" @>
        | other -> failwithf "Expected defect, got %A" other

    [<Fact>]
    let ``Flow timeout helpers work as expected`` () =
        let okResult = 
            Flow.sleep (TimeSpan.FromMilliseconds 50.0)
            |> Flow.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
            |> Flow.runSync ()
        test <@ okResult = Exit.Success () @>

        let errorResult =
            Flow.sleep (TimeSpan.FromMilliseconds 50.0)
            |> Flow.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> Flow.runSync ()
        test <@ errorResult = Exit.Failure (Cause.Fail "timed out") @>

        let withResult =
            Flow.sleep (TimeSpan.FromMilliseconds 50.0)
            |> Flow.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> Flow.succeed ())
            |> Flow.runSync ()
        test <@ withResult = Exit.Success () @>

    [<Fact>]
    let ``Flow timeout interrupts and awaits losing workflow cleanup`` () =
        let cleanedUp = ref false
        let operation : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeAsyncFinalizer (fun _ -> async {
                    do! Async.Sleep 10
                    cleanedUp.Value <- true
                })
                do! Flow.sleep (TimeSpan.FromSeconds 30.0)
            }

        let result =
            operation
            |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "timed out") @>
        test <@ cleanedUp.Value @>

    [<Fact>]
    let ``Flow cancellation helpers expose and check runtime token`` () =
        use cts = new CancellationTokenSource()
        cts.Cancel()

        let tokenResult =
            Flow.cancellationToken
            |> Flow.map (fun token -> token.IsCancellationRequested)
            |> Flow.runSyncWithToken () cts.Token

        let ensureResult : Exit<unit, string> =
            Flow.ensureNotCanceled
            |> Flow.runSyncWithToken () cts.Token

        let liveResult : Exit<unit, string> = Flow.ensureNotCanceled |> Flow.runSync ()

        test <@ tokenResult = Exit.Success true @>
        test <@ ensureResult = Exit.Failure Cause.Interrupt @>
        test <@ liveResult = Exit.Success () @>

    let private decide (Schedule op: Schedule<unit, int, 'output>) attempt =
        match Flow.runSync () (op 0 (ScheduleContext.ofAttempt attempt)) with
        | Exit.Success decision -> decision
        | other -> failwithf "Expected a decision, got %A" other

    let private ms (value: float) = TimeSpan.FromMilliseconds value

    [<Fact>]
    let ``Scheduling: union continues while either side continues and waits the shorter delay`` () =
        let schedule = Schedule.recurs 2 |> Schedule.union (Schedule.recurs 4)
        test <@ decide schedule 0 = (Some(Some 0, Some 0), TimeSpan.Zero) @>
        test <@ decide schedule 2 = (Some(None, Some 2), TimeSpan.Zero) @>
        test <@ decide schedule 4 = (None, TimeSpan.Zero) @>

        let shorter = Schedule.spaced (ms 1000.0) |> Schedule.union (Schedule.spaced (ms 3000.0))
        test <@ snd (decide shorter 0) = ms 1000.0 @>

        let capped = Schedule.exponential (ms 200.0) |> Schedule.union (Schedule.spaced (TimeSpan.FromSeconds 30.0))
        test <@ snd (decide capped 0) = ms 200.0 @>
        test <@ [ 0..40 ] |> List.forall (fun attempt -> snd (decide capped attempt) <= TimeSpan.FromSeconds 30.0) @>
        test <@ snd (decide capped 40) = TimeSpan.FromSeconds 30.0 @>

    [<Fact>]
    let ``Scheduling: intersect stops when either side stops and waits the longer delay`` () =
        let longer = Schedule.spaced (ms 1000.0) |> Schedule.intersect (Schedule.spaced (ms 3000.0))
        test <@ decide longer 0 = (Some(0, 0), ms 3000.0) @>

        let limited = Schedule.recurs 1 |> Schedule.intersect (Schedule.spaced (ms 10.0))
        test <@ fst (decide limited 1) = None @>

        let executions = ref 0

        let result : Exit<int, string> =
            Flow.delay (fun () ->
                executions.Value <- executions.Value + 1
                Flow.fail "transient")
            |> Flow.retry (Schedule.recurs 10 |> Schedule.intersect (Schedule.exponential (TimeSpan.FromTicks 1L)))
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "transient") @>
        test <@ executions.Value = 11 @>

    [<Fact>]
    let ``Scheduling: fixedRate aligns runs to the first run's start`` () =
        let delay started ended =
            Schedule.fixedRateDelay
                (ms 50.0)
                { Attempt = 0
                  LoopStarted = TimeSpan.Zero
                  ExecutionStarted = ms started
                  ExecutionEnded = ms ended
                  RunState = ScheduleContext.newRunState () }

        // A run's own duration does not push later runs back.
        test <@ delay 0.0 20.0 = ms 30.0 @>
        test <@ delay 50.0 70.0 = ms 30.0 @>
        // A timer that fires late or slightly early still belongs to its tick.
        test <@ delay 52.0 60.0 = ms 40.0 @>
        test <@ delay 49.5 60.0 = ms 40.0 @>
        test <@ delay 46.0 60.0 = ms 40.0 @>
        // A run that takes no time waits a whole period instead of running again at once.
        test <@ delay 50.0 50.0 = ms 50.0 @>

    [<Fact>]
    let ``Scheduling: fixedRate runs once immediately after an overrun, then realigns`` () =
        let delay started ended =
            Schedule.fixedRateDelay
                (ms 50.0)
                { Attempt = 0
                  LoopStarted = TimeSpan.Zero
                  ExecutionStarted = ms started
                  ExecutionEnded = ms ended
                  RunState = ScheduleContext.newRunState () }

        // The first run took more than two periods: run again at once...
        test <@ delay 0.0 130.0 = TimeSpan.Zero @>
        // ...and that immediate run realigns to the next boundary instead of catching up on missed ticks.
        test <@ delay 130.0 135.0 = ms 15.0 @>

        raises<ArgumentException> <@ Schedule.fixedRate TimeSpan.Zero @>

    [<Fact>]
    let ``Scheduling: repeat with fixedRate does not drift`` () =
        let starts = ResizeArray<TimeSpan>()

        let run : Flow<unit, Never, unit> =
            flow {
                let! now = runtimeNow ()
                starts.Add now
                do! Flow.sleep (ms 15.0)
            }

        let result = run |> Flow.repeat (Schedule.fixedRate (ms 40.0) |> Schedule.intersect (Schedule.recurs 4)) |> runOnManualTime
        test <@ result = Exit.Success () @>

        // Runs start on the 40 ms grid from the first start; drifting by the 15 ms run time would give 55 ms steps.
        test <@ starts |> Seq.map (fun start -> (start - starts[0]).TotalMilliseconds) |> List.ofSeq = [ 0.0; 40.0; 80.0; 120.0; 160.0 ] @>

    [<Fact>]
    let ``Scheduling: fixedRate after an overrun runs once immediately, then realigns`` () =
        let starts = ResizeArray<TimeSpan>()
        let durations = Collections.Generic.Queue<float>([ 130.0; 5.0; 5.0; 5.0 ])

        let run : Flow<unit, Never, unit> =
            flow {
                let! now = runtimeNow ()
                starts.Add now
                do! Flow.sleep (ms (if durations.Count > 0 then durations.Dequeue() else 5.0))
            }

        let result = run |> Flow.repeat (Schedule.fixedRate (ms 50.0) |> Schedule.intersect (Schedule.recurs 3)) |> runOnManualTime
        test <@ result = Exit.Success () @>

        // The first run overran two ticks (50, 100): the next starts at once (130), with no burst for the missed
        // ticks, and the one after that realigns to the 150 ms boundary.
        test <@ starts |> Seq.map (fun start -> (start - starts[0]).TotalMilliseconds) |> List.ofSeq = [ 0.0; 130.0; 150.0; 200.0 ] @>

    [<Fact>]
    let ``Flow.sleep waits exactly its delay on the runtime time source`` () =
        let elapsed =
            flow {
                let! before = runtimeNow ()
                do! Flow.sleep (TimeSpan.FromHours 1.0)
                let! after = runtimeNow ()
                return after - before
            }
            |> runOnManualTime

        test <@ elapsed = Exit.Success(TimeSpan.FromHours 1.0) @>

    [<Fact>]
    let ``Flow.retry waits each exponential delay before the next attempt`` () =
        let attempts = ResizeArray<TimeSpan>()

        let failing : Flow<unit, string, unit> =
            flow {
                let! now = runtimeNow ()
                attempts.Add now
                return! Flow.fail "transient"
            }

        let result = failing |> Flow.retry (Schedule.exponential (ms 100.0) |> Schedule.intersect (Schedule.recurs 3)) |> runOnManualTime
        test <@ result = Exit.Failure(Cause.Fail "transient") @>
        test <@ attempts |> Seq.map (fun at -> (at - attempts[0]).TotalMilliseconds) |> List.ofSeq = [ 0.0; 100.0; 300.0; 700.0 ] @>

    [<Fact>]
    let ``Flow.repeat with spaced waits after each run`` () =
        let starts = ResizeArray<TimeSpan>()

        let run : Flow<unit, Never, unit> =
            flow {
                let! now = runtimeNow ()
                starts.Add now
                do! Flow.sleep (ms 250.0)
            }

        let result = run |> Flow.repeat (Schedule.spaced (TimeSpan.FromSeconds 1.0) |> Schedule.intersect (Schedule.recurs 2)) |> runOnManualTime
        test <@ result = Exit.Success () @>
        // spaced waits after the run ends, so each start is run time plus spacing after the previous one.
        test <@ starts |> Seq.map (fun start -> (start - starts[0]).TotalMilliseconds) |> List.ofSeq = [ 0.0; 1250.0; 2500.0 ] @>

    [<Fact>]
    let ``Flow.timeout fires on the runtime time source`` () =
        let slow : Flow<unit, string, string> =
            flow {
                do! Flow.sleep (TimeSpan.FromHours 1.0)
                return "finished"
            }

        let timedOut = slow |> Flow.timeout (TimeSpan.FromSeconds 5.0) "timed out" |> runOnManualTime
        let inTime = slow |> Flow.timeout (TimeSpan.FromHours 2.0) "timed out" |> runOnManualTime
        test <@ timedOut = Exit.Failure(Cause.Fail "timed out") @>
        test <@ inTime = Exit.Success "finished" @>

    [<Fact>]
    let ``Schedule.whileInput retries only the errors it selects`` () =
        let runs = ref 0

        let result =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.fail (if runs.Value < 3 then "transient" else "permanent"))
            |> Flow.retry (Schedule.recurs 10 |> Schedule.whileInput (fun error -> error = "transient"))
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "permanent") @>
        test <@ runs.Value = 3 @>

    [<Fact>]
    let ``Schedule.untilInput stops on the input it names`` () =
        let runs = ref 0

        let result =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.ok runs.Value)
            |> Flow.repeat (Schedule.recurs 10 |> Schedule.untilInput (fun value -> value >= 4))
            |> Flow.runSync ()

        test <@ result = Exit.Success 4 @>

    [<Fact>]
    let ``Schedule.recursAtMost caps retries while keeping the schedule's delays`` () =
        let runs = ref 0

        let result : Exit<unit, string> =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.fail "boom")
            |> Flow.retry (Schedule.spaced TimeSpan.Zero |> Schedule.recursAtMost 2)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "boom") @>
        test <@ runs.Value = 3 @>

    [<Fact>]
    let ``Retry record counts retries and filters with When`` () =
        let runs = ref 0

        let retried : Exit<unit, string> =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.fail "boom")
            |> Flow.retry (Retry.schedule { Retry.defaults with Retries = 2; Backoff = Backoff.NoDelay })
            |> Flow.runSync ()

        test <@ retried = Exit.Failure (Cause.Fail "boom") @>
        test <@ runs.Value = 3 @>

        let filteredRuns = ref 0

        let filtered : Exit<unit, string> =
            Flow.delay(fun () ->
                filteredRuns.Value <- filteredRuns.Value + 1
                Flow.fail "fatal")
            |> Flow.retry (Retry.schedule { Retry.defaults with Backoff = Backoff.NoDelay; When = fun error -> error <> "fatal" })
            |> Flow.runSync ()

        test <@ filtered = Exit.Failure (Cause.Fail "fatal") @>
        test <@ filteredRuns.Value = 1 @>

    [<Fact>]
    let ``Retry record exponential backoff doubles and caps`` () =
        let (Schedule op) =
            Retry.schedule
                { Retry.defaults with
                    Retries = 10
                    Backoff = Backoff.Exponential(TimeSpan.FromMilliseconds 100.0, TimeSpan.FromMilliseconds 350.0) }
            : Schedule<unit, string, int>

        let delayAt attempt =
            match Flow.runSync () (op "e" (ScheduleContext.ofAttempt attempt)) with
            | Exit.Success(_, delay) -> delay
            | other -> failwithf "Expected a decision, got %A" other

        test <@ [ 0; 1; 2; 3 ] |> List.map delayAt = [ TimeSpan.FromMilliseconds 100.0; TimeSpan.FromMilliseconds 200.0; TimeSpan.FromMilliseconds 350.0; TimeSpan.FromMilliseconds 350.0 ] @>

    [<Fact>]
    let ``Retry releases a failed attempt's resources before the next attempt and keeps the successful one`` () =
        let released = ResizeArray<int>()
        let runs = ref 0
        let releasedWhenReturned = ref [||]

        let result =
            flow {
                let! value =
                    flow {
                        runs.Value <- runs.Value + 1
                        let attempt = runs.Value
                        do! Flow.scopeAsyncFinalizer (fun _ -> async { lock released (fun () -> released.Add attempt) })

                        if attempt < 3 then
                            return! Flow.fail "transient"
                        else
                            return attempt
                    }
                    |> Flow.retry (Schedule.recurs 5)

                releasedWhenReturned.Value <- lock released (fun () -> released.ToArray())
                return value
            }
            |> Flow.runSync ()

        test <@ result = Exit.Success 3 @>
        test <@ releasedWhenReturned.Value = [| 1; 2 |] @>
        test <@ lock released (fun () -> released.ToArray()) = [| 1; 2; 3 |] @>

    // Records when each attempt of a failing flow starts, relative to the first, on manual time.
    let private recordAttempts (attempts: ResizeArray<TimeSpan>) (work: TimeSpan) : Flow<unit, string, unit> =
        flow {
            let! now = runtimeNow ()
            attempts.Add now
            if work > TimeSpan.Zero then do! Flow.sleep work
            return! Flow.fail "transient"
        }

    let private offsets (attempts: ResizeArray<TimeSpan>) =
        attempts |> Seq.map (fun at -> (at - attempts[0]).TotalMilliseconds) |> List.ofSeq

    [<Fact>]
    let ``Schedule.andThen switches to the next schedule and restarts its count`` () =
        let attempts = ResizeArray<TimeSpan>()

        let schedule =
            Schedule.spaced (ms 10.0)
            |> Schedule.recursAtMost 2
            |> Schedule.andThen (Schedule.exponential (ms 100.0) |> Schedule.recursAtMost 2)

        let result = recordAttempts attempts TimeSpan.Zero |> Flow.retry schedule |> runOnManualTime

        test <@ result = Exit.Failure(Cause.Fail "transient") @>
        // Two quick retries 10 ms apart, then the exponential schedule from its own start: 100 ms, then 200 ms.
        test <@ offsets attempts = [ 0.0; 10.0; 20.0; 120.0; 320.0 ] @>

    [<Fact>]
    let ``Schedule.andThen reports which phase decided`` () =
        let (Schedule op) = Schedule.recurs 1 |> Schedule.andThen (Schedule.recurs 1)
        let runState = ScheduleContext.newRunState ()

        let decide attempt =
            match Flow.runSync () (op 0 { ScheduleContext.ofAttempt attempt with RunState = runState }) with
            | Exit.Success(decision, _) -> decision
            | other -> failwithf "Expected a decision, got %A" other

        test <@ decide 0 = Some(Choice1Of2 0) @>
        test <@ decide 1 = Some(Choice2Of2 0) @>
        test <@ decide 2 = None @>

    [<Fact>]
    let ``Schedule.whileOutput and untilOutput stop on the schedule's own output`` () =
        let attempts = ResizeArray<TimeSpan>()

        let schedule =
            Schedule.exponential (ms 100.0)
            |> Schedule.whileOutput (fun delay -> delay < ms 500.0)

        let result = recordAttempts attempts TimeSpan.Zero |> Flow.retry schedule |> runOnManualTime
        test <@ result = Exit.Failure(Cause.Fail "transient") @>
        // Delays 100, 200, 400; the next (800) is not below 500, so the retry stops.
        test <@ offsets attempts = [ 0.0; 100.0; 300.0; 700.0 ] @>

        let counted = ref 0
        let untilThird = Schedule.recurs 10 |> Schedule.untilOutput (fun attempt -> attempt >= 2)

        let repeated =
            Flow.delay (fun () -> counted.Value <- counted.Value + 1; Flow.ok ())
            |> Flow.repeat untilThird
            |> Flow.runSync ()

        test <@ repeated = Exit.Success () @>
        test <@ counted.Value = 3 @>

    [<Fact>]
    let ``Schedule.upTo stops once the time budget has passed`` () =
        let attempts = ResizeArray<TimeSpan>()
        let schedule = Schedule.spaced (ms 300.0) |> Schedule.upTo (TimeSpan.FromSeconds 1.0)

        let result = recordAttempts attempts (ms 50.0) |> Flow.retry schedule |> runOnManualTime
        test <@ result = Exit.Failure(Cause.Fail "transient") @>
        // Each attempt takes 50 ms and waits 300 ms: starts at 0, 350, 700, 1050. The fourth ends at 1100, past 1 s,
        // so no fifth attempt is scheduled.
        test <@ offsets attempts = [ 0.0; 350.0; 700.0; 1050.0 ] @>

    [<Fact>]
    let ``Schedule.elapsed and map report time since the first run`` () =
        let seen = ResizeArray<float>()

        let schedule =
            Schedule.elapsed
            |> Schedule.map (fun elapsed -> seen.Add elapsed.TotalMilliseconds; elapsed)
            |> Schedule.whileOutput (fun elapsed -> elapsed < ms 250.0)

        let work : Flow<unit, string, unit> = Flow.sleep (ms 100.0)
        let result = work |> Flow.repeat schedule |> runOnManualTime

        test <@ result = Exit.Success () @>
        test <@ List.ofSeq seen = [ 100.0; 200.0; 300.0 ] @>

    [<Fact>]
    let ``Schedule.resetAfter restores the restart budget after a healthy run`` () =
        let starts = ResizeArray<TimeSpan>()
        let runs = ref 0

        // Crashes quickly twice, then runs for an hour before crashing, then crashes quickly until the budget ends.
        let worker : Flow<unit, string, unit> =
            flow {
                let! now = runtimeNow ()
                starts.Add now
                runs.Value <- runs.Value + 1
                if runs.Value = 3 then do! Flow.sleep (TimeSpan.FromHours 1.0)
                return! Flow.die (InvalidOperationException "crash")
            }

        let schedule = Schedule.recurs 2 |> Schedule.resetAfter (TimeSpan.FromMinutes 10.0)
        let result = worker |> Flow.supervise schedule |> runOnManualTime

        test <@ match result with Exit.Failure(Cause.Die _) -> true | _ -> false @>
        // Without the reset the budget of 2 restarts ends after run 3; the healthy hour earns two more.
        test <@ runs.Value = 5 @>

    [<Fact>]
    let ``Flow.sleep never ends before its delay, even for fractions of a millisecond`` () =
        // Platform timers take whole milliseconds; rounding down would end a 1.5 ms sleep after 1 ms.
        let shortest =
            [ for _ in 1..40 ->
                  let stopwatch = Diagnostics.Stopwatch.StartNew()
                  Flow.sleep (TimeSpan.FromMilliseconds 1.5) |> Flow.runSync () |> ignore
                  stopwatch.Elapsed.TotalMilliseconds ]
            |> List.min

        test <@ shortest >= 1.5 @>
