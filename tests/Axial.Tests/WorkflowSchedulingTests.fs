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
            workflow |> Schedule.retry (Schedule.recurs 5)

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
            workflow |> Schedule.repeat (Schedule.recurs 3)

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

        let retryResult = retryWorkflow |> Schedule.retry (Schedule.recurs 3) |> Flow.runSync ()

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

        let repeatResult = repeatWorkflow |> Schedule.repeat (Schedule.recurs 3) |> Flow.runSync ()

        test <@ repeatCount = 4 @>
        test <@ repeatResult = Exit.Success 4 @>

        let mutable zeroRecursAttempts = 0
        let zeroRecursWorkflow : Flow<unit, string, string> =
            flow {
                zeroRecursAttempts <- zeroRecursAttempts + 1
                return! Flow.fail "fails"
            }

        zeroRecursWorkflow |> Schedule.retry (Schedule.recurs 0) |> Flow.runSync () |> ignore

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

            workflow |> Schedule.retry schedule |> Flow.runSync () |> ignore
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
            |> Schedule.retry (Schedule.recurs 5)
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
            |> Schedule.retry (Schedule.recurs 5)
            |> Flow.runSync ()

        test <@ interruptAttempts = 1 @>
        test <@ interruptResult = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Scheduling: repeat surfaces schedule evaluation failure as a defect`` () =
        let failingSchedule : Schedule<unit, int, int> =
            Schedule(fun _ _ -> Flow.fail ())

        let result : Exit<int, string> =
            Flow.ok 1
            |> Schedule.repeat failingSchedule
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
            |> Schedule.repeat (Schedule.spaced (TimeSpan.FromSeconds 30.0))
            |> Flow.runSyncWithToken () cts.Token

        test <@ repeatResult = Exit.Failure Cause.Interrupt @>

        use retryCts = new CancellationTokenSource()
        retryCts.CancelAfter(TimeSpan.FromMilliseconds 20.0)

        let retryResult : Exit<int, string> =
            Flow.fail "transient"
            |> Schedule.retry (Schedule.spaced (TimeSpan.FromSeconds 30.0))
            |> Flow.runSyncWithToken () retryCts.Token

        test <@ retryResult = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Scheduling: exponential caps instead of overflowing and rejects negative delays`` () =
        let (Schedule op) = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)

        let delayAt attempt =
            match Flow.runSync () (op 0 attempt) with
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
            match Flow.runSync () (op 0 attempt) with
            | Exit.Success (_, delay) -> delay
            | other -> failwithf "Expected a delay decision, got %A" other

        test <@ delayAt 0 = TimeSpan.FromMilliseconds 750.0 @>
        test <@ delayAt 5 = TimeSpan.FromMilliseconds 750.0 @>

        let (Schedule cappedOp) =
            Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
            |> Schedule.jitteredWith (fun () -> 0.999)

        let cappedDelay =
            match Flow.runSync () (cappedOp TimeSpan.Zero 100) with
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

            match Flow.runSync () (op 0 0) with
            | Exit.Success(_, delay) -> delay
            | other -> failwithf "Expected a delay decision, got %A" other

        test <@ delayWithSample (TimeSpan.FromSeconds 1.0) (fun () -> -1.0) = TimeSpan.Zero @>
        test <@ delayWithSample TimeSpan.MaxValue (fun () -> 5.0) = TimeSpan.MaxValue @>

    [<Fact>]
    let ``Flow runtime helpers cover timeout and retry`` () =
        let timeoutResult =
            Flow.Runtime.sleep (TimeSpan.FromMilliseconds 20.0)
            |> Flow.Runtime.timeout (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> Flow.runSync ()

        let retryRuns = ref 0

        let retryWorkflow =
            let policy : RetryPolicy<string> =
                { MaxAttempts = 3
                  Delay = fun _ -> TimeSpan.Zero
                  ShouldRetry = fun error -> error = "transient" }

            Flow.delay(fun () ->
                retryRuns.Value <- retryRuns.Value + 1

                if retryRuns.Value < 2 then
                    Flow.fail "transient"
                else
                    Flow.succeed 42)
            |> Flow.Runtime.retry policy

        let retryResult =
            retryWorkflow
            |> Flow.runSync ()

        test <@ timeoutResult = Exit.Failure (Cause.Fail "timed out") @>
        test <@ retryResult = Exit.Success 42 @>
        test <@ retryRuns.Value = 2 @>

    [<Fact>]
    let ``Flow retry does not retry defects or interruptions`` () =
        let retryRuns = ref 0

        let policy : RetryPolicy<string> =
            { MaxAttempts = 3
              Delay = fun _ -> TimeSpan.Zero
              ShouldRetry = fun _ -> true }

        let defectResult =
            Flow.delay(fun () ->
                retryRuns.Value <- retryRuns.Value + 1
                Flow.die (InvalidOperationException "boom"))
            |> Flow.Runtime.retry policy
            |> Flow.runSync ()

        test <@ retryRuns.Value = 1 @>
        match defectResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "boom" @>
        | other -> failwithf "Expected defect, got %A" other

    [<Fact>]
    let ``Flow timeout helpers work as expected`` () =
        let okResult = 
            Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> Flow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
            |> Flow.runSync ()
        test <@ okResult = Exit.Success () @>

        let errorResult =
            Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> Flow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> Flow.runSync ()
        test <@ errorResult = Exit.Failure (Cause.Fail "timed out") @>

        let withResult =
            Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> Flow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> Flow.succeed ())
            |> Flow.runSync ()
        test <@ withResult = Exit.Success () @>

    [<Fact>]
    let ``Flow timeout interrupts and awaits losing workflow cleanup`` () =
        let cleanedUp = ref false
        let operation : Flow<unit, string, unit> =
            flow {
                do! Flow.addFinalizerAsync (fun _ -> async {
                    do! Async.Sleep 10
                    cleanedUp.Value <- true
                })
                do! Flow.Runtime.sleep (TimeSpan.FromSeconds 30.0)
            }

        let result =
            operation
            |> Flow.Runtime.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "timed out") @>
        test <@ cleanedUp.Value @>

    [<Fact>]
    let ``Flow cancellation helpers expose and check runtime token`` () =
        use cts = new CancellationTokenSource()
        cts.Cancel()

        let tokenResult =
            Flow.Runtime.cancellationToken
            |> Flow.map (fun token -> token.IsCancellationRequested)
            |> Flow.runSyncWithToken () cts.Token

        let ensureResult =
            Flow.Runtime.ensureNotCanceled "canceled"
            |> Flow.runSyncWithToken () cts.Token

        test <@ tokenResult = Exit.Success true @>
        test <@ ensureResult = Exit.Failure (Cause.Fail "canceled") @>
