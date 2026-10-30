namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
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
        let left : Layer<unit, string, int> = Layer.fromFlow (Flow.fail "left")
        let right : Layer<unit, string, int> = Layer.fromFlow (Flow.fail "right")

        let workflow =
            Flow.env<int * int, string>
            |> Layer.provide (Layer.zipPar left right)

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Both(Cause.Fail "left", Cause.Fail "right")) @>

    [<Fact>]
    let ``Flow scoped resource sequences use failure before release defect`` () =
        let releaseDefect = InvalidOperationException "release failed"

        let workflow =
            Flow.scopeAcquireRelease
                (Flow.succeed "resource")
                (fun _ -> Flow.die releaseDefect)
            |> Flow.bind (fun _ -> Flow.fail "use failed")
            |> Flow.scoped

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Then(Cause.Fail "use failed", Cause.Die releaseDefect)) @>

    [<Fact>]
    let ``Flow run sequences workflow failure before runtime finalizer defect`` () =
        let finalizerDefect = InvalidOperationException "finalizer failed"

        let workflow =
            flow {
                do! Flow.scopeFinalizer (Flow.die finalizerDefect)
                return! Flow.fail "workflow failed"
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Then(Cause.Fail "workflow failed", Cause.Die finalizerDefect)) @>

    [<Fact>]
    let ``Fiber metadata records identity parent and lifecycle status`` () =
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let workflow =
            flow {
                let! fiber =
                    flow {
                        do! Flow.fromTask (fun _ -> release.Task)
                        return 42
                    }
                    |> Flow.fork

                let initial = Fiber.dump fiber
                release.SetResult(())
                let! value = Fiber.join fiber
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
                        do! Flow.sleep (TimeSpan.FromSeconds 5.0)
                        return 42
                    }
                    |> Flow.fork

                let! interrupted = Fiber.interrupt fiber
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
                    Fiber.join fiber
                    |> Flow.foldCause
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
                let! value = Fiber.join grandchild
                return grandchildDump, value
            }

        let workflow =
            flow {
                let! child = Flow.fork childWorkflow
                let childDump = Fiber.dump child
                let! grandchildDump, value = Fiber.join child
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
                        do! Flow.sleep (TimeSpan.FromSeconds 5.0)
                        return 42
                    }
                    |> Flow.fork

                let! interrupted = Fiber.interrupt fiber

                let! joined =
                    Fiber.join fiber
                    |> Flow.foldCause
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
                        do! Flow.sleep (TimeSpan.FromSeconds 5.0)
                        return 42
                    }
                    |> Flow.fork

                capturedFiber <- Some fiber
                do! Flow.sleep (TimeSpan.FromSeconds 5.0)
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
                do!
                    Flow.scopeFinalizer (
                        Flow.cancellationToken
                        |> Flow.map (fun token -> calls.Add $"first:{token.IsCancellationRequested}")
                    )

                do!
                    Flow.scopeFinalizer (
                        Flow.cancellationToken
                        |> Flow.map (fun token -> calls.Add $"second:{token.IsCancellationRequested}")
                    )

                do! Flow.sleep (TimeSpan.FromSeconds 5.0)
            }

        cts.CancelAfter(TimeSpan.FromMilliseconds 50.0)
        let result = Flow.runSyncWithToken () cts.Token workflow

        test <@ result = Exit.Failure Cause.Interrupt @>
        // Releases run in reverse order and are not interrupted by the cancellation that closed the scope.
        test <@ List.ofSeq calls = [ "second:False"; "first:False" ] @>

    [<Fact>]
    let ``Cancellation failure is sequenced before finalizer defect`` () =
        use cts = new CancellationTokenSource()
        let finalizerDefect = InvalidOperationException "cleanup failed"

        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (Flow.die finalizerDefect)
                do! Flow.sleep (TimeSpan.FromSeconds 5.0)
            }

        cts.CancelAfter(TimeSpan.FromMilliseconds 50.0)
        let result = Flow.runSyncWithToken () cts.Token workflow

        test <@ result = Exit.Failure(Cause.Then(Cause.Interrupt, Cause.Die finalizerDefect)) @>

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``While checks the guard once per iteration and stops immediately on failure`` (suspending: bool) =
        let mutable checks = 0
        let mutable visited = 0
        let mutable cleaned = 0
        let guard () =
            checks <- checks + 1
            visited < 10
        let workflow : Flow<unit, string, unit> =
            flow {
                while guard () do
                    if suspending then
                        do! Flow.fromTask (fun _ -> task { do! Task.Yield() })
                    visited <- visited + 1
                    if visited = 3 then return! Flow.fail "stop"
            }
            |> Flow.ensuring (Flow.delay (fun () -> cleaned <- cleaned + 1; Flow.ok ()))

        test <@ workflow.RunSynchronously(()) = Exit.Failure(Cause.Fail "stop") @>
        test <@ visited = 3 && checks = 3 && cleaned = 1 @>

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``While reevaluates its delayed body and preserves the outer runtime`` (suspending: bool) =
        let mutable checks = 0
        let mutable visited = 0
        let guard () =
            checks <- checks + 1
            visited < 10
        let workflow : Flow<unit, string, unit> =
            flow {
                while guard () do
                    let expected = visited
                    let! _ =
                        flow {
                            if suspending then
                                do! Flow.fromTask (fun _ -> task { do! Task.Yield() })
                            let! annotations = Flow.annotations
                            test <@ annotations["region"] = "inner" @>
                        }
                        |> Flow.annotate "region" "inner"
                    let! annotations = Flow.annotations
                    test <@ annotations["region"] = "outer" @>
                    test <@ visited = expected @>
                    visited <- visited + 1
            }
            |> Flow.annotate "region" "outer"

        test <@ workflow.RunSynchronously(()) = Exit.Success () @>
        test <@ visited = 10 && checks = 11 @>

    [<Theory>]
    [<InlineData(false, false)>]
    [<InlineData(false, true)>]
    [<InlineData(true, false)>]
    [<InlineData(true, true)>]
    let ``Execution boundary closes once and combines outcomes across completion modes``
        (pendingBody: bool, pendingCleanup: bool) =
        task {
            for outcome in 0..2 do
                for cleanupFails in [false; true] do
                    let bodyError = InvalidOperationException "body"
                    let cleanupError = InvalidOperationException "cleanup"
                    let bodySignal = TaskCompletionSource<Exit<int, string>>(TaskCreationOptions.RunContinuationsAsynchronously)
                    let cleanupSignal = TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
                    let cleanupStarted = TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
                    let mutable closed = 0
                    let bodyExit = if outcome = 1 then Exit.Failure(Cause.Fail "expected") else Exit.Success 42
                    // Raw execution tests exceptions at the executor boundary, before an interop adapter classifies them.
                    let workflow : Flow<unit, string, int> =
                        Flow(fun _ _ ->
                            RuntimeState.current().Scope.AddFinalizer(fun _ ->
                                closed <- closed + 1
                                cleanupStarted.SetResult()
                                if pendingCleanup then cleanupSignal.Task
                                elif cleanupFails then raise cleanupError
                                else Task.CompletedTask)
                            if pendingBody then ValueTask<Exit<int, string>>(bodySignal.Task)
                            elif outcome = 2 then raise bodyError
                            else ValueTask<Exit<int, string>>(bodyExit))

                    let running = workflow.StartAsValueTask(())
                    if pendingBody then
                        let completed = running.IsCompleted
                        test <@ not completed @>
                        test <@ closed = 0 @>
                        if outcome = 2 then bodySignal.SetException bodyError
                        else bodySignal.SetResult bodyExit
                    do! cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds 10.0)
                    if pendingCleanup then
                        let completed = running.IsCompleted
                        test <@ not completed @>
                        if cleanupFails then cleanupSignal.SetException cleanupError
                        else cleanupSignal.SetResult()
                    let! exit = running
                    let primary = if outcome = 2 then Exit.Failure(Cause.Die bodyError) else bodyExit
                    let expected =
                        if cleanupFails then
                            match primary with
                            | Exit.Success _ -> Exit.Failure(Cause.Die cleanupError)
                            | Exit.Failure cause -> Exit.Failure(Cause.Then(cause, Cause.Die cleanupError))
                        else primary
                    test <@ exit = expected @>
                    test <@ closed = 1 @>
        }

    // T28b: an OperationCanceledException the runtime did not ask for is a defect at every boundary, not an
    // interruption nobody requested; foldCause already classifies it so, and the root, fork, retry and Layer.zipPar
    // boundaries now agree. Each case throws it synchronously and after an await.
    let private foreignCancellation () = OperationCanceledException "foreign"

    let private throwsForeign (afterAwait: bool) (error: exn) : Flow<unit, string, int> =
        if afterAwait then
            Flow.fromTask (fun _ -> task { do! Task.Yield() })
            |> Flow.bind (fun () -> Flow.delay (fun () -> raise error))
        else
            Flow.delay (fun () -> raise error)

    let private boundaries (afterAwait: bool) (error: exn) : (string * Flow<unit, string, int>) list =
        [ "root", throwsForeign afterAwait error
          "foldCause",
          throwsForeign afterAwait error |> Flow.foldCause Flow.ok (fun cause -> Flow.ofExit (Exit.Failure cause))
          "fork",
          flow {
              let! fiber = Flow.fork (throwsForeign afterAwait error)
              return! Fiber.join fiber
          }
          "retry", throwsForeign afterAwait error |> Flow.retry (Schedule.recurs 2)
          "Layer.zipPar",
          Flow.env<int * int, string>
          |> Layer.provide (Layer.zipPar (Layer.fromFlow (throwsForeign afterAwait error)) (Layer.fromFlow (Flow.ok 2)))
          |> Flow.map fst ]

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``a foreign OperationCanceledException is a defect at every boundary`` (afterAwait: bool) =
        let error = foreignCancellation ()

        let outcomes =
            boundaries afterAwait error
            |> List.map (fun (name, source) ->
                let running = Task.Run(fun () -> Flow.runSync () source)
                Assert.True(running.Wait(TimeSpan.FromSeconds 10.0), $"{name} did not settle.")
                name, running.Result)

        let expected = boundaries afterAwait error |> List.map (fun (name, _) -> name, Exit.Failure(Cause.Die error))
        test <@ outcomes = expected @>
