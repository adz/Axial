namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowResourceTests =
    [<Fact>]
    let ``Flow runtime helpers cover release`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            Flow.Runtime.useWithAcquireRelease
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> Flow.fail "boom")
            |> Flow.runSync ()

        test <@ acquireReleaseResult = Exit.Failure (Cause.Fail "boom") @>
        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow runtime helpers release after defects`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            Flow.Runtime.useWithAcquireRelease
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> Flow.die (InvalidOperationException "boom"))
            |> Flow.runSync ()

        match acquireReleaseResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "boom" @>
        | other -> failwithf "Expected defect, got %A" other
        test <@ releaseCount.Value = 1 @>
