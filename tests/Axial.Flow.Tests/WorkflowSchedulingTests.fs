namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
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
