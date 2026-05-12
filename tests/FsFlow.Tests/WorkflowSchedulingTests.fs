namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowSchedulingTests =
    [<Fact>]
    let ``Scheduling: retry failing flow`` () =
        let mutable attempts = 0
        let workflow =
            flow {
                attempts <- attempts + 1
                if attempts < 3 then
                    return! Flow.fail "try again"
                else
                    return "success"
            }

        let retried = Flow.Retry(workflow, Schedule.recurs 5)
        let result = Flow.runSync () retried
        
        test <@ result = Exit.Success "success" @>
        test <@ attempts = 3 @>

    [<Fact>]
    let ``Scheduling: repeat successful flow`` () =
        let mutable count = 0
        let workflow =
            flow {
                count <- count + 1
                return count
            }

        let repeated = Flow.Repeat(workflow, Schedule.recurs 3)
        let result = Flow.runSync () repeated
        
        test <@ result = Exit.Success 4 @>
        test <@ count = 4 @>

    [<Fact>]
    let ``AsyncFlow runtime helpers cover timeout and retry`` () =
        let timeoutResult =
            AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 20.0)
            |> AsyncFlow.Runtime.timeout (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> AsyncFlow.run ()
            |> Async.RunSynchronously

        let retryRuns = ref 0

        let retryWorkflow =
            let policy : RetryPolicy<string> =
                { MaxAttempts = 3
                  Delay = fun _ -> TimeSpan.Zero
                  ShouldRetry = fun error -> error = "transient" }

            AsyncFlow.delay(fun () ->
                retryRuns.Value <- retryRuns.Value + 1

                if retryRuns.Value < 2 then
                    AsyncFlow.fail "transient"
                else
                    AsyncFlow.succeed 42)
            |> AsyncFlow.Runtime.retry policy

        let retryResult =
            retryWorkflow
            |> AsyncFlow.run ()
            |> Async.RunSynchronously

        test <@ timeoutResult = Exit.Failure (Cause.Fail "timed out") @>
        test <@ retryResult = Exit.Success 42 @>
        test <@ retryRuns.Value = 2 @>

    [<Fact>]
    let ``TaskFlow runtime helpers cover timeout and retry`` () =
        let timeoutResult =
            TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 20.0)
            |> TaskFlow.Runtime.timeout (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> TaskFlow.run () CancellationToken.None
            |> fun task -> task.GetAwaiter().GetResult()

        let retryRuns = ref 0

        let retryWorkflow =
            let policy : RetryPolicy<string> =
                { MaxAttempts = 3
                  Delay = fun _ -> TimeSpan.Zero
                  ShouldRetry = fun error -> error = "transient" }

            TaskFlow.delay(fun () ->
                retryRuns.Value <- retryRuns.Value + 1

                if retryRuns.Value < 2 then
                    TaskFlow.fail "transient"
                else
                    TaskFlow.succeed 42)
            |> TaskFlow.Runtime.retry policy

        let retryResult =
            retryWorkflow
            |> TaskFlow.run () CancellationToken.None
            |> fun task -> task.GetAwaiter().GetResult()

        test <@ timeoutResult = Exit.Failure (Cause.Fail "timed out") @>
        test <@ retryResult = Exit.Success 42 @>
        test <@ retryRuns.Value = 2 @>

    [<Fact>]
    let ``AsyncFlow timeout helpers work as expected`` () =
        let okResult = 
            AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> AsyncFlow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
            |> AsyncFlow.run ()
            |> Async.RunSynchronously
        test <@ okResult = Exit.Success () @>

        let errorResult =
            AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> AsyncFlow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> AsyncFlow.run ()
            |> Async.RunSynchronously
        test <@ errorResult = Exit.Failure (Cause.Fail "timed out") @>

        let withResult =
            AsyncFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> AsyncFlow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> AsyncFlow.succeed ())
            |> AsyncFlow.run ()
            |> Async.RunSynchronously
        test <@ withResult = Exit.Success () @>

    [<Fact>]
    let ``TaskFlow timeout helpers work as expected`` () =
        let okResult = 
            TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> TaskFlow.Runtime.timeoutToOk (TimeSpan.FromMilliseconds 1.0) ()
            |> TaskFlow.run () CancellationToken.None
            |> fun t -> t.GetAwaiter().GetResult()
        test <@ okResult = Exit.Success () @>

        let errorResult =
            TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> TaskFlow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 1.0) "timed out"
            |> TaskFlow.run () CancellationToken.None
            |> fun t -> t.GetAwaiter().GetResult()
        test <@ errorResult = Exit.Failure (Cause.Fail "timed out") @>

        let withResult =
            TaskFlow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            |> TaskFlow.Runtime.timeoutWith (TimeSpan.FromMilliseconds 1.0) (fun () -> TaskFlow.succeed ())
            |> TaskFlow.run () CancellationToken.None
            |> fun t -> t.GetAwaiter().GetResult()
        test <@ withResult = Exit.Success () @>
