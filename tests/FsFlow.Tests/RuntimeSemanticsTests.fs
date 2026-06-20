namespace FsFlow.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Result
open Axial.Validation
open Swensen.Unquote
open Xunit

module RuntimeSemanticsTests =
    [<Fact>]
    let ``Cause maps and reports composite failures without dropping structure`` () =
        let defect = InvalidOperationException "boom"
        let cause =
            Cause.both
                (Cause.thenCause (Cause.Fail "left") (Cause.Die defect))
                (Cause.traced "right branch" Cause.Interrupt)

        let mapped = Cause.map String.length cause

        test <@ Cause.failures mapped = [ 4 ] @>
        test <@ Cause.defects mapped = [ defect ] @>
        test <@ Cause.isInterrupted mapped @>

        let rendered = Cause.prettyPrint id cause
        test <@ rendered.Contains "Both" @>
        test <@ rendered.Contains "Then" @>
        test <@ rendered.Contains "Traced(right branch)" @>

    [<Fact>]
    let ``Exit toResult refuses to flatten composite failures`` () =
        let exit =
            Exit.Failure(Cause.both (Cause.Fail "left") (Cause.Fail "right"))

        let raised =
            try
                Exit.toResult exit |> ignore
                false
            with :? InvalidOperationException ->
                true

        test <@ raised @>

    [<Fact>]
    let ``Flow zipPar accumulates simultaneous branch failures`` () =
        let workflow =
            Flow.zipPar (Flow.fail "left") (Flow.fail "right")

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Both(Cause.Fail "left", Cause.Fail "right")) @>

    [<Fact>]
    let ``Layer zipPar accumulates parallel provisioning failures`` () =
        let left : Layer<unit, string, int> = Layer.fromValueTask (fun _ _ -> Execution.ofError "left")
        let right : Layer<unit, string, int> = Layer.fromValueTask (fun _ _ -> Execution.ofError "right")

        let workflow =
            Flow.env<int * int, string>
            |> Flow.provide (Layer.zipPar left right)

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Both(Cause.Fail "left", Cause.Fail "right")) @>

    [<Fact>]
    let ``Flow acquireReleaseWith sequences use failure before release defect`` () =
        let releaseDefect = InvalidOperationException "release failed"

        let workflow =
            Flow.acquireReleaseWith
                (Flow.succeed "resource")
                (fun _ _ -> Task.FromException releaseDefect)
                (fun _ -> Flow.fail "use failed")

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Then(Cause.Fail "use failed", Cause.Die releaseDefect)) @>

    [<Fact>]
    let ``Flow run sequences workflow failure before runtime finalizer defect`` () =
        let finalizerDefect = InvalidOperationException "finalizer failed"

        let workflow =
            flow {
                do! Flow.addFinalizer(fun _ -> Task.FromException finalizerDefect)
                return! Flow.fail "workflow failed"
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Then(Cause.Fail "workflow failed", Cause.Die finalizerDefect)) @>

    [<Fact>]
    let ``Fiber metadata records identity parent and lifecycle status`` () =
        let workflow =
            flow {
                let! fiber =
                    flow {
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 20.0)
                        return 42
                    }
                    |> Flow.fork

                let initial = Fiber.dump fiber
                let! value = Flow.join fiber
                let completed = Fiber.dump fiber
                return initial, completed, value
            }

        let result = Flow.runSync () workflow

        match result with
        | Exit.Success(initial, completed, value) ->
            test <@ value = 42 @>
            test <@ initial.Id.Value > 0L @>
            test <@ initial.ParentId.IsSome @>
            test <@ initial.Status = FiberStatus.Running @>
            test <@ completed.Status = FiberStatus.Succeeded @>
            test <@ completed.StartedAt = initial.StartedAt @>
        | other ->
            failwithf "Expected successful fiber metadata result, got %A" other

    [<Fact>]
    let ``Fiber metadata records interruption status`` () =
        let workflow =
            flow {
                let! fiber =
                    flow {
                        do! Flow.Runtime.sleep (TimeSpan.FromSeconds 5.0)
                        return 42
                    }
                    |> Flow.fork

                let! interrupted = Flow.interrupt fiber
                return Fiber.dump fiber, interrupted
            }

        let result = Flow.runSync () workflow

        match result with
        | Exit.Success(dump, Exit.Failure Cause.Interrupt) ->
            test <@ dump.Status = FiberStatus.Interrupted @>
        | other ->
            failwithf "Expected interrupted fiber metadata result, got %A" other

    [<Fact>]
    let ``Fiber metadata records defect status`` () =
        let defect = InvalidOperationException "child defect"

        let workflow =
            flow {
                let! fiber = Flow.fork (Flow.die defect)

                let! joined =
                    Flow.join fiber
                    |> Flow.fold
                        (Exit.Success >> Flow.succeed)
                        (Exit.Failure >> Flow.succeed)

                return Fiber.dump fiber, joined
            }

        let result = Flow.runSync () workflow

        match result with
        | Exit.Success(dump, Exit.Failure(Cause.Die error)) ->
            test <@ obj.ReferenceEquals(error, defect) @>
            test <@ dump.Status = FiberStatus.Failed @>
        | other ->
            failwithf "Expected child defect and failed metadata, got %A" other

    [<Fact>]
    let ``Nested fibers record parent child identity`` () =
        let childWorkflow =
            flow {
                let! grandchild =
                    flow {
                        return 42
                    }
                    |> Flow.fork

                let grandchildDump = Fiber.dump grandchild
                let! value = Flow.join grandchild
                return grandchildDump, value
            }

        let workflow =
            flow {
                let! child = Flow.fork childWorkflow
                let childDump = Fiber.dump child
                let! grandchildDump, value = Flow.join child
                return childDump, grandchildDump, value
            }

        let result = Flow.runSync () workflow

        match result with
        | Exit.Success(childDump, grandchildDump, value) ->
            test <@ value = 42 @>
            test <@ childDump.ParentId.IsSome @>
            test <@ grandchildDump.ParentId = Some childDump.Id @>
        | other ->
            failwithf "Expected nested fiber identity result, got %A" other

    [<Fact>]
    let ``Joining an interrupted fiber preserves interruption`` () =
        let workflow =
            flow {
                let! fiber =
                    flow {
                        do! Flow.Runtime.sleep (TimeSpan.FromSeconds 5.0)
                        return 42
                    }
                    |> Flow.fork

                let! interrupted = Flow.interrupt fiber

                let! joined =
                    Flow.join fiber
                    |> Flow.fold
                        (Exit.Success >> Flow.succeed)
                        (Exit.Failure >> Flow.succeed)

                return interrupted, joined, Fiber.dump fiber
            }

        let result = Flow.runSync () workflow

        match result with
        | Exit.Success(Exit.Failure Cause.Interrupt, Exit.Failure Cause.Interrupt, dump) ->
            test <@ dump.Status = FiberStatus.Interrupted @>
        | other ->
            failwithf "Expected interrupted join to preserve Cause.Interrupt, got %A" other

    [<Fact>]
    let ``Parent cancellation propagates to forked child`` () =
        use cts = new CancellationTokenSource()
        let mutable capturedFiber : Fiber<string, int> option = None

        let workflow : Flow<unit, string, unit> =
            flow {
                let! fiber =
                    flow {
                        do! Flow.Runtime.sleep (TimeSpan.FromSeconds 5.0)
                        return 42
                    }
                    |> Flow.fork

                capturedFiber <- Some fiber
                do! Flow.Runtime.sleep (TimeSpan.FromSeconds 5.0)
            }

        cts.CancelAfter(TimeSpan.FromMilliseconds 50.0)
        let result = Flow.runSyncWithToken () cts.Token workflow

        match result, capturedFiber with
        | Exit.Failure Cause.Interrupt, Some fiber ->
            let childExit = fiber.ExitTask.GetAwaiter().GetResult()
            test <@ childExit = Exit.Failure Cause.Interrupt @>
            test <@ (Fiber.dump fiber).Status = FiberStatus.Interrupted @>
        | other, _ ->
            failwithf "Expected parent cancellation to interrupt parent and child, got %A" other

    [<Fact>]
    let ``Runtime finalizers run in reverse order under cancellation`` () =
        use cts = new CancellationTokenSource()
        let calls = ResizeArray<string>()

        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.addFinalizer(fun token ->
                    calls.Add $"first:{token.IsCancellationRequested}"
                    Task.CompletedTask)

                do! Flow.addFinalizer(fun token ->
                    calls.Add $"second:{token.IsCancellationRequested}"
                    Task.CompletedTask)

                do! Flow.Runtime.sleep (TimeSpan.FromSeconds 5.0)
            }

        cts.CancelAfter(TimeSpan.FromMilliseconds 50.0)
        let result = Flow.runSyncWithToken () cts.Token workflow

        test <@ result = Exit.Failure Cause.Interrupt @>
        test <@ List.ofSeq calls = [ "second:True"; "first:True" ] @>

    [<Fact>]
    let ``Cancellation failure is sequenced before finalizer defect`` () =
        use cts = new CancellationTokenSource()
        let finalizerDefect = InvalidOperationException "cleanup failed"

        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.addFinalizer(fun _ -> Task.FromException finalizerDefect)
                do! Flow.Runtime.sleep (TimeSpan.FromSeconds 5.0)
            }

        cts.CancelAfter(TimeSpan.FromMilliseconds 50.0)
        let result = Flow.runSyncWithToken () cts.Token workflow

        test <@ result = Exit.Failure(Cause.Then(Cause.Interrupt, Cause.Die finalizerDefect)) @>
