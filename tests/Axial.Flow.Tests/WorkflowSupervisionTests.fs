namespace Axial.Tests

open System
open Axial.Flow
open Swensen.Unquote
open Xunit

module WorkflowSupervisionTests =
    [<Fact>]
    let ``Supervise restarts a flow that dies until it succeeds`` () =
        let runs = ref 0

        let result =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1

                if runs.Value < 3 then
                    Flow.die (InvalidOperationException "boom")
                else
                    Flow.succeed 42)
            |> Flow.Runtime.supervise (SupervisePolicy.noDelay 5)
            |> Flow.runSync ()

        test <@ result = Exit.Success 42 @>
        test <@ runs.Value = 3 @>

    [<Fact>]
    let ``Supervise propagates the final defect when attempts are exhausted`` () =
        let runs = ref 0

        let result =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.die (InvalidOperationException "boom"))
            |> Flow.Runtime.supervise (SupervisePolicy.noDelay 3)
            |> Flow.runSync ()

        test <@ runs.Value = 3 @>
        match result with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "boom" @>
        | other -> failwithf "Expected defect, got %A" other

    [<Fact>]
    let ``Supervise does not restart typed errors or interruptions`` () =
        let failRuns = ref 0

        let failResult =
            Flow.delay(fun () ->
                failRuns.Value <- failRuns.Value + 1
                Flow.fail "domain error")
            |> Flow.Runtime.supervise (SupervisePolicy.noDelay 3)
            |> Flow.runSync ()

        test <@ failResult = Exit.Failure (Cause.Fail "domain error") @>
        test <@ failRuns.Value = 1 @>

        let interruptRuns = ref 0

        let interruptResult : Exit<unit, string> =
            Flow.delay(fun () ->
                interruptRuns.Value <- interruptRuns.Value + 1
                Flow.ofExit (Exit.Failure Cause.Interrupt))
            |> Flow.Runtime.supervise (SupervisePolicy.noDelay 3)
            |> Flow.runSync ()

        test <@ interruptResult = Exit.Failure Cause.Interrupt @>
        test <@ interruptRuns.Value = 1 @>

    [<Fact>]
    let ``Supervise consults ShouldRestart with the defect exception`` () =
        let runs = ref 0

        let policy =
            { MaxAttempts = 5
              Delay = fun _ -> TimeSpan.Zero
              ShouldRestart = fun error -> error.Message = "restartable" }

        let result : Exit<int, string> =
            Flow.delay(fun () ->
                runs.Value <- runs.Value + 1
                Flow.die (InvalidOperationException "fatal"))
            |> Flow.Runtime.supervise policy
            |> Flow.runSync ()

        test <@ runs.Value = 1 @>
        match result with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "fatal" @>
        | other -> failwithf "Expected defect, got %A" other

    [<Fact>]
    let ``Supervise runs each attempt in its own scope and releases finalizers between attempts`` () =
        let releases = ResizeArray<int>()
        let runs = ref 0

        let workflow : Flow<unit, string, string> =
            flow {
                runs.Value <- runs.Value + 1
                let attempt = runs.Value

                do! Flow.addFinalizerAsync (fun _ -> async { releases.Add attempt })

                if attempt < 3 then
                    return! Flow.die (InvalidOperationException "boom")
                else
                    return "success"
            }

        let result =
            workflow
            |> Flow.Runtime.supervise (SupervisePolicy.noDelay 5)
            |> Flow.runSync ()

        test <@ result = Exit.Success "success" @>
        // The failed attempts' finalizers ran before the next attempt started, not at the end.
        test <@ List.ofSeq releases |> List.truncate 2 = [ 1; 2 ] @>
        test <@ releases.Count = 3 @>

    [<Fact>]
    let ``Supervise rejects a policy with no attempts`` () =
        raises<ArgumentException>
            <@ Flow.succeed 1 |> Flow.Runtime.supervise (SupervisePolicy.noDelay 0) @>
