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
    type TrackingDisposable(events: ResizeArray<string>, name: string) =
        member _.Name = name

        interface IDisposable with
            member _.Dispose() =
                events.Add($"{name}:disposed")

    [<Fact>]
    let ``flow use covers lexical cleanup`` () =
        let events = ResizeArray<string>()

        let workflow =
            flow {
                use resource = new TrackingDisposable(events, "resource")
                events.Add($"{resource.Name}:used")
                return resource.Name
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success "resource" @>
        test <@ List.ofSeq events = [ "resource:used"; "resource:disposed" ] @>

    [<Fact>]
    let ``Flow acquireReleaseWith releases after typed failure`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            Flow.acquireReleaseWith
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> Flow.fail "boom")
            |> Flow.runSync ()

        test <@ acquireReleaseResult = Exit.Failure (Cause.Fail "boom") @>
        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow acquireReleaseWith releases after defects`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            Flow.acquireReleaseWith
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

    [<Fact>]
    let ``Flow acquireReleaseWith releases after interruption`` () =
        let releaseCount = ref 0
        let interrupted = Flow(fun _ _ -> EffectFlow.ofInterrupt ())

        let acquireReleaseResult =
            Flow.acquireReleaseWith
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> interrupted)
            |> Flow.runSync ()

        test <@ acquireReleaseResult = Exit.Failure Cause.Interrupt @>
        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow acquireReleaseWith releases once when release defects`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            Flow.acquireReleaseWith
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.FromException(InvalidOperationException "release failed"))
                (fun _ -> Flow.succeed "ok")
            |> Flow.runSync ()

        match acquireReleaseResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "release failed" @>
        | other -> failwithf "Expected release defect, got %A" other

        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow acquireRelease keeps resource alive until runtime scope closes`` () =
        let events = ResizeArray<string>()

        let workflow =
            flow {
                let! resource =
                    Flow.acquireRelease
                        (Flow.succeed "resource")
                        (fun name _ ->
                            events.Add($"{name}:released")
                            Task.CompletedTask)

                events.Add($"{resource}:after-acquire")

                do!
                    flow {
                        events.Add($"{resource}:subflow")
                    }

                events.Add($"{resource}:after-subflow")
                return resource
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success "resource" @>
        test <@ List.ofSeq events = [ "resource:after-acquire"; "resource:subflow"; "resource:after-subflow"; "resource:released" ] @>

    [<Fact>]
    let ``Flow scoped finalizers close in deterministic reverse order`` () =
        let events = ResizeArray<string>()

        let workflow =
            flow {
                do! Flow.addFinalizer(fun _ ->
                    events.Add "first"
                    Task.CompletedTask)

                do! Flow.addFinalizer(fun _ ->
                    events.Add "second"
                    Task.CompletedTask)
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success () @>
        test <@ List.ofSeq events = [ "second"; "first" ] @>

    [<Fact>]
    let ``Layer acquireRelease releases when provided flow completes`` () =
        let events = ResizeArray<string>()

        let layer =
            Layer.acquireRelease
                (Layer.succeed "service")
                (fun service _ ->
                    events.Add($"{service}:released")
                    Task.CompletedTask)

        let workflow =
            flow {
                let! service = Flow.env<string, string>
                events.Add($"{service}:used")
                return service
            }

        let result =
            workflow
            |> Flow.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Success "service" @>
        test <@ List.ofSeq events = [ "service:used"; "service:released" ] @>

    [<Fact>]
    let ``Layer acquireRelease releases successful branch when parallel sibling fails`` () =
        let events = ResizeArray<string>()

        let successful =
            Layer.acquireRelease
                (Layer.succeed "service")
                (fun service _ ->
                    events.Add($"{service}:released")
                    Task.CompletedTask)

        let failed =
            Layer.effect (fun (_, _) _ -> EffectFlow.ofError "failed")

        let result =
            Flow.env<string * string, string>
            |> Flow.provide (Layer.merge successful failed)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "failed") @>
        test <@ List.ofSeq events = [ "service:released" ] @>
