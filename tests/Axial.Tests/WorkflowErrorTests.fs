namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowErrorTests =
    type private DomainError =
        | MissingValue
        | UpstreamError of string

    [<Fact>]
    let ``fold lets interruption pass through without running the typed error handler`` () =
        let mutable handlerRan = false

        let workflow =
            flow {
                let! started = Deferred.make<unit, string, unit> ()

                let source : Flow<unit, string, int> =
                    flow {
                        let! _ = Deferred.succeed () started
                        do! Flow.sleep (TimeSpan.FromSeconds 30.0)
                        return 42
                    }

                let folded : Flow<unit, Never, int> =
                    source
                    |> Flow.fold Flow.ok (fun _ ->
                        handlerRan <- true
                        Flow.ok 0)

                let! fiber = Flow.fork folded
                do! Deferred.await started
                let! exit = Fiber.interrupt fiber
                return exit, handlerRan
            }

        let result = Flow.runSync () workflow
        test <@ result = Exit.Success(Exit.Failure Cause.Interrupt, false) @>

    [<Fact>]
    let ``fold lets a defect pass through without running the typed error handler`` () =
        let defect = InvalidOperationException "broken"
        let mutable handlerRan = false
        let source : Flow<unit, string, int> = Flow.die defect

        let result : Exit<int, Never> =
            source
            |> Flow.fold Flow.ok (fun _ ->
                handlerRan <- true
                Flow.ok 0)
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Die defect) @>
        test <@ not handlerRan @>

    [<Fact>]
    let ``fold sends a typed failure to the error handler`` () =
        let source : Flow<unit, string, int> = Flow.fail "missing"
        let result = source |> Flow.fold (string >> Flow.ok) (fun error -> Flow.ok $"error: {error}") |> Flow.runSync ()
        test <@ result = Exit.Success "error: missing" @>

    [<Fact>]
    let ``recover changes error type on a traced typed failure`` () =
        let source : Flow<unit, string, int> = Flow.fail "missing" |> Flow.tracedError "load"
        let result : Exit<int, int> = source |> Flow.recover (fun error -> Flow.fail error.Length) |> Flow.runSync ()
        test <@ result = Exit.Failure(Cause.Fail 7) @>

    [<Fact>]
    let ``recover returns its fallback error`` () =
        let source : Flow<unit, string, int> = Flow.fail "missing"
        let result : Exit<int, int> = source |> Flow.recover (fun _ -> Flow.fail 9) |> Flow.runSync ()
        test <@ result = Exit.Failure(Cause.Fail 9) @>

    [<Fact>]
    let ``recover leaves success unchanged`` () =
        let source : Flow<unit, string, int> = Flow.ok 42
        let result : Exit<int, int> = source |> Flow.recover (fun _ -> Flow.fail 9) |> Flow.runSync ()
        test <@ result = Exit.Success 42 @>

    [<Fact>]
    let ``recover passes defects through`` () =
        let defect = Exception "broken"
        let source : Flow<unit, string, int> = Flow.die defect
        let result : Exit<int, int> = source |> Flow.recover (fun _ -> Flow.ok 0) |> Flow.runSync ()
        test <@ result = Exit.Failure(Cause.Die defect) @>

    [<Fact>]
    let ``recover passes interruption through`` () =
        let source : Flow<unit, string, int> = Flow.ofExit (Exit.Failure Cause.Interrupt)
        let result : Exit<int, int> = source |> Flow.recover (fun _ -> Flow.ok 0) |> Flow.runSync ()
        test <@ result = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``recover does not invoke handler for compound causes`` () =
        let defect = Exception "broken"
        let source : Flow<unit, string, int> =
            Flow.ofExit (Exit.Failure(Cause.Both(Cause.Fail "missing", Cause.Die defect)))
        let mutable invoked = false
        let result : Exit<int, int> =
            source |> Flow.recover (fun _ -> invoked <- true; Flow.ok 0) |> Flow.runSync ()
        test <@ not invoked @>
        match result with
        | Exit.Failure(Cause.Both(Cause.Die converted, Cause.Die original)) ->
            let wrapped = Assert.IsType<UnrecoveredFailureException>(converted)
            test <@ wrapped.Value :?> string = "missing" && wrapped.Message.Contains("missing") && obj.ReferenceEquals(original, defect) @>
        | _ -> failwith "Expected both causes to remain"

    [<Fact>]
    let ``recover retypes deeply nested causes without stack overflow`` () =
        let mutable cause = Cause.Fail "original"
        for _ in 1 .. 100000 do
            cause <- Cause.Traced(cause, "trace")
        let source : Flow<unit, string, int> = Flow.ofExit (Exit.Failure(Cause.Both(cause, Cause.Interrupt)))
        let result : Exit<int, int> = source |> Flow.recover (fun _ -> Flow.ok 0) |> Flow.runSync ()
        let mutable current =
            match result with
            | Exit.Failure(Cause.Both(left, Cause.Interrupt)) -> left
            | _ -> failwith "Expected compound cause"
        for _ in 1 .. 100000 do
            match current with
            | Cause.Traced(inner, _) -> current <- inner
            | _ -> failwith "Expected trace"
        match current with
        | Cause.Die (:? UnrecoveredFailureException as wrapped) -> test <@ wrapped.Value :?> string = "original" @>
        | _ -> failwith "Expected retained failure"

    [<Fact>]
    let ``recover handler exceptions become defects`` () =
        let thrown = InvalidOperationException "handler"
        let source : Flow<unit, string, int> = Flow.fail "missing"
        let result : Exit<int, int> = source |> Flow.recover (fun _ -> raise thrown) |> Flow.runSync ()
        test <@ result = Exit.Failure(Cause.Die thrown) @>

    [<Fact>]
    let ``recover handler cancellation with cancelled runtime token interrupts`` () =
        use cancellation = new CancellationTokenSource()
        let source : Flow<unit, string, int> = Flow.fail "missing"
        let result : Exit<int, int> =
            source |> Flow.recover (fun _ -> cancellation.Cancel(); raise (OperationCanceledException()))
            |> Flow.runSyncWithToken () cancellation.Token
        test <@ result = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Flow composition helpers cover error tapping fallback and pairing`` () =
        let tappedErrors = ResizeArray<string>()

        let tapPreservesOriginalError =
            Flow.fail "primary"
            |> Flow.tapError (fun error ->
                tappedErrors.Add error
                Flow.succeed ())
            |> Flow.runSync ()

        let tapSkipsSuccess =
            Flow.succeed 42
            |> Flow.tapError (fun error ->
                tappedErrors.Add $"unexpected:{error}"
                Flow.succeed ())
            |> Flow.runSync ()

        let recovered =
            Flow.fail "missing"
            |> Flow.orElse (Flow.envWith (fun env -> env + 1))
            |> Flow.runSync 41

        let bypassesFallback =
            Flow.succeed 10
            |> Flow.orElse (Flow.succeed 99)
            |> Flow.runSync ()

        let zipped =
            Flow.zip (Flow.envWith (fun env -> env + 1)) (Flow.envWith (fun env -> env * 2))
            |> Flow.runSync 5

        let mapped =
            Flow.map2 (+) (Flow.envWith (fun env -> env + 1)) (Flow.envWith (fun env -> env * 2))
            |> Flow.runSync 5

        test <@ tapPreservesOriginalError = Exit.Failure (Cause.Fail "primary") @>
        test <@ tapSkipsSuccess = Exit.Success 42 @>
        test <@ List.ofSeq tappedErrors = [ "primary" ] @>
        test <@ recovered = Exit.Success 42 @>
        test <@ bypassesFallback = Exit.Success 10 @>
        test <@ zipped = Exit.Success(6, 10) @>
        test <@ mapped = Exit.Success 16 @>

    [<Fact>]
    let ``Flow die creates a defect cause`` () =
        let defect = InvalidOperationException "boom"

        let result =
            Flow.die defect
            |> Flow.runSync ()

        match result with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

    [<Fact>]
    let ``attempt constructors translate exceptions to recoverable typed failures`` () =
        let asyncDefect = InvalidOperationException "async boom"
        let taskDefect = InvalidOperationException "task boom"
        let valueTaskDefect = InvalidOperationException "value task boom"

        let fromTaskResult =
            Task.FromException<int>(taskDefect)
            |> Flow.awaitStartedTask
            |> Flow.runSync ()

        let attemptTaskResult =
            Task.FromException<int>(taskDefect)
            |> Flow.attemptStartedTask
            |> Flow.runSync ()

        let attemptValueTaskResult =
            ValueTask<int>(Task.FromException<int>(valueTaskDefect))
            |> Flow.attemptStartedValueTask
            |> Flow.runSync ()

        let attemptAsyncResult =
            async { return raise asyncDefect }
            |> Flow.attemptAsync
            |> Flow.runSync ()

        let attemptCancellationResult =
            Task.FromCanceled<int>(CancellationToken(true))
            |> Flow.attemptStartedTask
            |> Flow.runSync ()

        match fromTaskResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, taskDefect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match attemptTaskResult with
        | Exit.Failure (Cause.Fail ex) -> test <@ obj.ReferenceEquals(ex, taskDefect) @>
        | other -> failwithf "Expected typed exception failure, got %A" other

        match attemptValueTaskResult with
        | Exit.Failure (Cause.Fail ex) -> test <@ obj.ReferenceEquals(ex, valueTaskDefect) @>
        | other -> failwithf "Expected typed exception failure, got %A" other

        match attemptAsyncResult with
        | Exit.Failure (Cause.Fail ex) -> test <@ obj.ReferenceEquals(ex, asyncDefect) @>
        | other -> failwithf "Expected typed exception failure, got %A" other

        // The task cancelled for its own reasons while the runtime token was live: that is a failure, not an
        // interruption anybody requested.
        match attemptCancellationResult with
        | Exit.Failure (Cause.Fail (:? OperationCanceledException)) -> ()
        | other -> failwithf "Expected a typed cancellation failure, got %A" other

        use cts = new CancellationTokenSource(TimeSpan.FromMilliseconds 20.0)

        let requestedCancellation =
            Flow.attemptTask (fun token -> task { do! Task.Delay(Timeout.Infinite, token) })
            |> Flow.runSyncWithToken () cts.Token

        test <@ requestedCancellation = Exit.Failure Cause.Interrupt @>

    // T28: foldCause and mapBoth must observe an exception thrown by the source flow, as Flow.catch does.
    let private dieHandler (defect: exn) (cause: Cause<string>) : Flow<unit, string, int> =
        match Cause.untraced cause with
        | Cause.Die error when obj.ReferenceEquals(error, defect) -> Flow.succeed -1
        | other -> Flow.succeed -2

    [<Fact>]
    let ``Flow foldCause sends a synchronously thrown exception to the failure handler as Die`` () =
        let defect = InvalidOperationException "boom"

        let thrownByBuilder : Exit<int, string> =
            flow { return raise defect }
            |> Flow.foldCause Flow.succeed (dieHandler defect)
            |> Flow.runSync ()

        let thrownByInvoke : Exit<int, string> =
            Flow(fun (_: unit) _ -> raise defect)
            |> Flow.foldCause Flow.succeed (dieHandler defect)
            |> Flow.runSync ()

        test <@ thrownByBuilder = Exit.Success -1 @>
        test <@ thrownByInvoke = Exit.Success -1 @>

    [<Fact>]
    let ``Flow foldCause sends an exception thrown after an async hop to the failure handler as Die`` () =
        let defect = InvalidOperationException "boom after hop"

        let afterSleep : Exit<int, string> =
            Flow.sleep (TimeSpan.FromMilliseconds 5.0)
            |> Flow.bind (fun () -> raise defect)
            |> Flow.foldCause Flow.succeed (dieHandler defect)
            |> Flow.runSync ()

        let afterTask : Exit<int, string> =
            flow {
                do! Flow.fromTask (fun _ -> task { do! Task.Yield() })
                return raise defect
            }
            |> Flow.foldCause Flow.succeed (dieHandler defect)
            |> Flow.runSync ()

        test <@ afterSleep = Exit.Success -1 @>
        test <@ afterTask = Exit.Success -1 @>

    [<Fact>]
    let ``Flow foldCause keeps a throw from the success handler out of the failure handler`` () =
        let defect = InvalidOperationException "success handler"
        let mutable failureHandlerRan = false

        let result : Exit<int, string> =
            Flow.succeed 1
            |> Flow.foldCause
                (fun _ -> raise defect)
                (fun _ ->
                    failureHandlerRan <- true
                    Flow.succeed -1)
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Die defect) @>
        test <@ not failureHandlerRan @>

    [<Fact>]
    let ``Flow foldCause reports a runtime cancellation thrown by the source as Interrupt`` () =
        use cancellation = new CancellationTokenSource()

        let result : Exit<string, string> =
            Flow(fun (_: unit) _ ->
                cancellation.Cancel()
                raise (OperationCanceledException cancellation.Token))
            |> Flow.foldCause
                (fun _ -> Flow.succeed "success")
                (fun cause ->
                    match Cause.untraced cause with
                    | Cause.Interrupt -> Flow.succeed "interrupt"
                    | other -> Flow.succeed $"other: %A{other}")
            |> Flow.runSyncWithToken () cancellation.Token

        test <@ result = Exit.Success "interrupt" @>

    [<Fact>]
    let ``Flow mapBoth maps an exception thrown by the source as Die`` () =
        let defect = InvalidOperationException "boom"

        let result : Exit<int, string> =
            flow { return raise defect }
            |> Flow.mapBoth id (fun cause ->
                match Cause.untraced cause with
                | Cause.Die error when obj.ReferenceEquals(error, defect) -> Cause.Fail "mapped"
                | other -> Cause.Fail "unmapped")
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "mapped") @>

    [<Fact>]
    let ``flow try finally runs the compensation when the body throws`` () =
        let defect = InvalidOperationException "body threw"
        let mutable synchronousRuns = 0
        let mutable asyncRuns = 0

        let synchronous : Exit<int, string> =
            flow {
                try
                    return raise defect
                finally
                    synchronousRuns <- synchronousRuns + 1
            }
            |> Flow.runSync ()

        let afterHop : Exit<int, string> =
            flow {
                try
                    do! Flow.fromTask (fun _ -> task { do! Task.Yield() })
                    return raise defect
                finally
                    asyncRuns <- asyncRuns + 1
            }
            |> Flow.runSync ()

        test <@ synchronous = Exit.Failure(Cause.Die defect) @>
        test <@ afterHop = Exit.Failure(Cause.Die defect) @>
        test <@ synchronousRuns = 1 @>
        test <@ asyncRuns = 1 @>

    [<Fact>]
    let ``flow use disposes the resource when the body throws`` () =
        let defect = InvalidOperationException "body threw"
        let mutable disposed = 0
        let resource () = { new IDisposable with member _.Dispose() = disposed <- disposed + 1 }

        let result : Exit<int, string> =
            flow {
                use _ = resource ()
                return raise defect
            }
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Die defect) @>
        test <@ disposed = 1 @>

    [<Fact>]
    let ``flow try with catches an exception thrown after an async hop`` () =
        let defect = InvalidOperationException "after hop"

        let result : Exit<string, string> =
            flow {
                try
                    do! Flow.fromTask (fun _ -> task { do! Task.Yield() })
                    return raise defect
                with error ->
                    return $"caught {error.Message}"
            }
            |> Flow.runSync ()

        test <@ result = Exit.Success "caught after hop" @>

    [<Fact>]
    let ``Flow catch converts simple defects and preserves typed failures and interruptions`` () =
        let defect = InvalidOperationException "boom"
        let mapper (ex: exn) = $"caught:{ex.Message}"

        let caughtDefect =
            Flow.die defect
            |> Flow.catch mapper
            |> Flow.runSync ()

        let typedFailure =
            Flow.fail "domain"
            |> Flow.catch mapper
            |> Flow.runSync ()

        let interrupted =
            Flow.ofExit (Exit.Failure Cause.Interrupt)
            |> Flow.catch mapper
            |> Flow.runSync ()

        let compound =
            Flow.ofExit (Exit.Failure (Cause.Then(Cause.Die defect, Cause.Fail "domain")))
            |> Flow.catch mapper
            |> Flow.runSync ()

        test <@ caughtDefect = Exit.Failure (Cause.Fail "caught:boom") @>
        test <@ typedFailure = Exit.Failure (Cause.Fail "domain") @>
        test <@ interrupted = Exit.Failure Cause.Interrupt @>

        match compound with
        | Exit.Failure (Cause.Then(Cause.Die ex, Cause.Fail "domain")) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected compound cause to be preserved, got %A" other

    [<Fact>]
    let ``Runtime boundaries classify defects and cancellation`` () =
        let defect = InvalidOperationException "boom"
        let canceled = OperationCanceledException "stop"

        let flowDefect =
            Flow.delay (fun () -> raise defect)
            |> Flow.runSync ()

        let flowCanceled =
            Flow.delay (fun () -> raise canceled)
            |> Flow.runSync ()

        let asyncDefect =
            flow {
                let! value = (async { return raise defect } : Async<int>)
                return value
            }
            |> Flow.runSync ()

        let asyncCanceled =
            flow {
                let! value = (async { return raise canceled } : Async<int>)
                return value
            }
            |> Flow.runSync ()

        let taskDefect =
            flow {
                let! value = Task.FromException<int>(defect) |> Flow.awaitStartedTask
                return value
            }
            |> Flow.runSync ()

        let taskCanceled =
            flow {
                let! value = Task.FromCanceled<int>(CancellationToken(true)) |> Flow.awaitStartedTask
                return value
            }
            |> Flow.runSync ()

        match flowDefect with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        test <@ flowCanceled = Exit.Failure Cause.Interrupt @>

        match asyncDefect with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match asyncCanceled with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, canceled) @>
        | other -> failwithf "Expected a cancellation defect, got %A" other

        match taskDefect with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        // Foreign cancellation the runtime did not request is a defect.
        match taskCanceled with
        | Exit.Failure (Cause.Die (:? OperationCanceledException)) -> ()
        | other -> failwithf "Expected a cancellation defect, got %A" other

        use cts = new CancellationTokenSource(TimeSpan.FromMilliseconds 20.0)

        let requestedCancellation : Exit<unit, string> =
            Flow.fromTask (fun token -> task { do! Task.Delay(Timeout.Infinite, token) })
            |> Flow.runSyncWithToken () cts.Token

        test <@ requestedCancellation = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Defects survive combinators boundaries and retry`` () =
        let defect = InvalidOperationException "boom"

        let mapErrorResult =
            Flow.die defect
            |> Flow.mapError String.length
            |> Flow.runSync ()

        let orElseResult =
            Flow.die defect
            |> Flow.orElse (Flow.succeed 99)
            |> Flow.runSync ()

        let zipResult =
            Flow.zip (Flow.die defect) (Flow.succeed 42)
            |> Flow.runSync ()

        let asyncBoundaryResult =
            flow {
                let! value = (async { return raise defect } : Async<int>)
                return value
            }
            |> Flow.runSync ()

        let taskBoundaryResult =
            flow {
                let! value = Task.FromException<int>(defect) |> Flow.awaitStartedTask
                return value
            }
            |> Flow.runSync ()

        let retryAttempts = ref 0

        let retryResult =
            let workflow : Flow<unit, string, int> =
                Flow.delay (fun () ->
                    retryAttempts.Value <- retryAttempts.Value + 1
                    raise defect)

            let retried : Flow<unit, string, int> =
                workflow |> Flow.retry (Schedule.recurs 5)

            retried
            |> Flow.runSync ()

        match mapErrorResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match orElseResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match zipResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match asyncBoundaryResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match taskBoundaryResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        match retryResult with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        test <@ retryAttempts.Value = 1 @>

    [<Fact>]
    let ``a unit-error result takes its error from the environment only on failure`` () =
        let errorRuns = ref 0

        let errorFlow =
            Flow.envWith (fun env ->
                errorRuns.Value <- errorRuns.Value + 1
                $"flow:{env}")

        let failed = Error() |> Flow.orElseFlow errorFlow |> Flow.runSync "env"
        let succeeded = Ok "value" |> Flow.orElseFlow errorFlow |> Flow.runSync "env"

        test <@ failed = Exit.Failure (Cause.Fail "flow:env") @>
        test <@ succeeded = Exit.Success "value" @>
        test <@ errorRuns.Value = 1 @>

    [<Fact>]
    let ``option and valueoption inputs short-circuit with unit errors across builders`` () =
        let syncSome : Flow<int, unit, int> =
            flow {
                let! env = Flow.env
                let! value = Some(env + 1)
                return value * 2
            }

        let syncNone : Flow<int, unit, int> =
            flow {
                let! env = Flow.env
                let! value = None
                return env + value
            }

        let syncValueSome : Flow<int, unit, int> =
            flow {
                let! env = Flow.env
                let! value = ValueSome(env + 1)
                return value * 2
            }

        let syncValueNone : Flow<int, unit, int> =
            flow {
                let! env = Flow.env
                let! value = ValueNone
                return env + value
            }

        let asyncWorkflow : Flow<int, unit, int> =
            flow {
                let! env = Flow.env
                let! value = Some(env + 1)
                let! extra = ValueSome(value + 1)
                return extra * 2
            }

        let asyncReturnFromNone : Flow<unit, unit, int> =
            flow { return! None }

        let taskWorkflow : Flow<int, unit, int> =
            flow {
                let! env = Flow.env
                let! value = Some(env + 1)
                let! extra = ValueSome(value + 1)
                return extra * 2
            }

        let taskReturnFromValueNone : Flow<unit, unit, int> =
            flow { return! ValueNone }

        test <@ Flow.runSync 20 syncSome = Exit.Success 42 @>
        test <@ Flow.runSync 20 syncNone = Exit.Failure (Cause.Fail ()) @>
        test <@ Flow.runSync 20 syncValueSome = Exit.Success 42 @>
        test <@ Flow.runSync 20 syncValueNone = Exit.Failure (Cause.Fail ()) @>
        test <@ Flow.runSync 19 asyncWorkflow = Exit.Success 42 @>
        test <@ Flow.runSync () asyncReturnFromNone = Exit.Failure (Cause.Fail ()) @>
        test <@ Flow.runSync 19 taskWorkflow = Exit.Success 42 @>
        test <@ Flow.runSync () taskReturnFromValueNone = Exit.Failure (Cause.Fail ()) @>

    [<Fact>]
    let ``explicit option adapters support custom workflow errors across modules`` () =
        let syncSome =
            Some 21
            |> Flow.fromOption "missing value"
            |> Flow.map ((*) 2)
            |> Flow.runSync ()

        let syncNone =
            None
            |> Flow.fromOption "missing value"
            |> Flow.runSync ()

        let syncValueSome =
            ValueSome 21
            |> Flow.fromValueOption "missing value"
            |> Flow.map ((*) 2)
            |> Flow.runSync ()

        let syncValueNone =
            ValueNone
            |> Flow.fromValueOption "missing value"
            |> Flow.runSync ()

        test <@ syncSome = Exit.Success 42 @>
        test <@ syncNone = Exit.Failure (Cause.Fail "missing value") @>
        test <@ syncValueSome = Exit.Success 42 @>
        test <@ syncValueNone = Exit.Failure (Cause.Fail "missing value") @>

    [<Fact>]
    let ``Bind assigns errors in all flow families`` () =
        let successOption : int option = Some 42
        let successValueOption : int voption = ValueSome 10
        let successCheck : Result<unit, unit> = Ok ()
        let asyncOption : Async<int option> = async { return Some 42 }
        let asyncValueOption : Async<int voption> = async { return ValueSome 10 }
        let asyncCheck : Async<Result<unit, unit>> = async { return Ok () }
        let successTaskOption : Task<int option> = Task.FromResult(Some 5)
        let successTaskCheck : Task<Result<unit, unit>> = Task.FromResult(Ok ())
        let successTaskValueOption : ValueTask<int voption> = ValueTask.FromResult(ValueSome 3)
        let successValueTaskCheck : ValueTask<Result<unit, unit>> = ValueTask.FromResult(Ok ())

        let flowTest =
            flow {
                let! x = successOption |> Bind.error "missing-option"
                let! y = successValueOption |> Bind.error "missing-voption"
                do! successCheck |> Bind.error "check-failed"
                return x + y
            }

        let asyncFlowTest =
            flow {
                let! (x : int) = asyncOption |> Bind.error "missing-option"
                let! (y : int) = asyncValueOption |> Bind.error "missing-voption"
                do! asyncCheck |> Bind.error "check-failed"
                return x + y
            }

        let taskFlowTest =
            flow {
                let! x = successOption |> Bind.error "missing-option"
                let! y = successValueOption |> Bind.error "missing-voption"
                do!
                    Ok ()
                    |> Bind.error "check-failed"
                let! z = successTaskOption |> Bind.error "task-missing"
                do! successTaskCheck |> Bind.error "task-check-failed"
                let! w = successTaskValueOption |> Bind.error "vtask-missing"
                do! successValueTaskCheck |> Bind.error "vtask-check-failed"
                return x + y + z + w
            }

        let flowResult = Flow.runSync () flowTest
        let asyncFlowResult = Flow.runSync () asyncFlowTest
        let taskFlowResult = Flow.runSync () taskFlowTest

        test <@ flowResult = Exit.Success 52 @>
        test <@ asyncFlowResult = Exit.Success 52 @>
        test <@ taskFlowResult = Exit.Success 60 @>

    [<Fact>]
    let ``Bind adapters retain their public BindError type and route errors`` () =
        let requireValue (source: int option) : BindError<unit, DomainError, int> =
            source |> Bind.error MissingValue

        let mapFailure (source: Result<int, string>) : BindError<unit, DomainError, int> =
            source |> Bind.mapError UpstreamError

        let missingWorkflow : Flow<unit, DomainError, int> =
            flow {
                let! value = requireValue None
                return value
            }

        let upstreamWorkflow : Flow<unit, DomainError, int> =
            flow {
                let! value = mapFailure (Error "denied")
                return value
            }

        test <@ Flow.runSync () missingWorkflow = Exit.Failure(Cause.Fail MissingValue) @>
        test <@ Flow.runSync () upstreamWorkflow = Exit.Failure(Cause.Fail(UpstreamError "denied")) @>

    [<Fact>]
    let ``Flow async syntax uses Bind assignment and mapping`` () =
        let tryGetUser username = async { return if username = "missing" then None else Some username }
        let isPwdValid password user = password = $"{user}-pwd"
        let authorize user = async { return if user = "blocked" then Error "denied" else Ok () }
        let createAuthToken user = if user = "expired" then Error "token-expired" else Ok $"token-{user}"

        let login username password =
            flow {
                let! (user : string) =
                    tryGetUser username
                    |> Bind.error InvalidUser

                do!
                    (if isPwdValid password user then Ok () else Error ())
                    |> Bind.error InvalidPwd

                do!
                    authorize user
                    |> Bind.mapError Unauthorized

                return!
                    createAuthToken user
                    |> Bind.mapError TokenErr
            }

        let success = Flow.runSync () (login "alice" "alice-pwd")
        let authFailure = Flow.runSync () (login "blocked" "blocked-pwd")
        let tokenFailure = Flow.runSync () (login "expired" "expired-pwd")

        test <@ success = Exit.Success "token-alice" @>
        test <@ authFailure = Exit.Failure (Cause.Fail (Unauthorized "denied")) @>
        test <@ tokenFailure = Exit.Failure (Cause.Fail (TokenErr "token-expired")) @>

    [<Fact>]
    let ``Nested carrier results enter the typed error channel`` () =
        let hasNestedConversion (carrier: Type) =
            typeof<FlowBindable>.GetMethods()
            |> Array.exists (fun methodInfo ->
                let parameters = methodInfo.GetParameters()

                if methodInfo.Name <> "Convert" || parameters.Length <> 2 then
                    false
                else
                    let sourceType = parameters.[1].ParameterType

                    if sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() = carrier then
                        let innerType = sourceType.GetGenericArguments().[0]
                        innerType.IsGenericType
                        && innerType.GetGenericTypeDefinition() = typedefof<Result<_, _>>
                    else
                        false)

        let sourceCount =
            typeof<FlowBuilder>.GetMethods()
            |> Array.filter (fun methodInfo -> methodInfo.Name = "Source")
            |> Array.length

        test <@ sourceCount = 1 @>
        test <@ hasNestedConversion typedefof<Async<_>> @>
        test <@ hasNestedConversion typedefof<ColdTask<_>> @>

        let asyncSource : Async<Result<int, string>> = async { return Error "async-error" }

        let coldTaskSource : ColdTask<Result<int, string>> =
            ColdTask(fun _ -> Task.FromResult(Error "cold-task-error"))

        let asyncWorkflow : Flow<unit, string, int> =
            flow {
                let! value = asyncSource |> Flow.fromAsyncResult
                return value
            }

        let coldTaskWorkflow : Flow<unit, string, int> =
            flow {
                let! value = coldTaskSource |> Flow.fromColdTaskResult
                return value
            }

        let asyncResult = Flow.runSync () asyncWorkflow
        let coldTaskResult = Flow.runSync () coldTaskWorkflow

        test <@ asyncResult = Exit.Failure (Cause.Fail "async-error") @>
        test <@ coldTaskResult = Exit.Failure (Cause.Fail "cold-task-error") @>

    [<Fact>]
    let ``Bind mapError stays symmetric across flow families`` () =
        let asyncSource : Async<Result<int, string>> = async { return Error "async-source" }
        let taskSource : Task<Result<int, string>> = task { return Error "task-source" }
        let asyncSuccess : Async<Result<int, string>> = async { return Ok 1 }

        let asyncMapped =
            flow {
                let! value =
                    asyncSource
                    |> Bind.mapError (fun error -> $"mapped-{error}")

                return value + 1
            }

        let taskMapped =
            flow {
                let! (asyncValue : int) =
                    asyncSuccess
                    |> Bind.mapError (fun error -> $"mapped-{error}")

                let! (taskValue : int) =
                    taskSource
                    |> Bind.mapError (fun error -> $"mapped-{error}")

                return asyncValue + taskValue
            }

        test <@ Flow.runSync () asyncMapped = Exit.Failure (Cause.Fail "mapped-async-source") @>
        test <@ Flow.runSync () taskMapped = Exit.Failure (Cause.Fail "mapped-task-source") @>

    [<Fact>]
    let ``Bind error fails correctly for check-like sources`` () =
        let missingOption : int option = None

        let flowFail = flow {
            let! (value : int) =
                missingOption
                |> Bind.error "failed"

            return value
        }

        let asyncFlowFail = flow {
            let! (value : int) =
                async { return ValueNone }
                |> Bind.error "failed"

            return value
        }

        let taskFlowFail = flow {
            let! (value : int) =
                missingOption
                |> Bind.error "failed"

            return value
        }

        test <@ Flow.runSync () flowFail = Exit.Failure (Cause.Fail "failed") @>
        test <@ Flow.runSync () asyncFlowFail = Exit.Failure (Cause.Fail "failed") @>
        test <@ Flow.runSync () taskFlowFail = Exit.Failure (Cause.Fail "failed") @>

    [<Fact>]
    let ``ToValueTask catches synchronous exception and returns Exit.Failure`` () =
        let defect = InvalidOperationException "sync boom"
        let badFlow = Flow(fun env ct -> raise defect)
        let vt = badFlow.StartAsValueTask(())
        let exit = vt.GetAwaiter().GetResult()
        match exit with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

    [<Fact>]
    let ``Recovery combinators see a typed failure or defect through its trace`` () =
        let traced : Flow<unit, string, int> = Flow.fail "boom" |> Flow.tracedError "while loading"
        let tracedDefect : Flow<unit, string, int> = Flow.die (InvalidOperationException "defect") |> Flow.tracedError "while loading"
        let tapped = ref None
        let attempts = ref 0

        let retried : Flow<unit, string, int> =
            Flow.delay (fun () ->
                attempts.Value <- attempts.Value + 1
                if attempts.Value < 3 then Flow.fail "transient" |> Flow.tracedError "attempt" else Flow.ok attempts.Value)
            |> Flow.retry (Schedule.recurs 5)

        test <@ traced |> Flow.orElseWith (fun error -> Flow.ok error.Length) |> Flow.runSync () = Exit.Success 4 @>
        test <@ traced |> Flow.orElse (Flow.ok 0) |> Flow.runSync () = Exit.Success 0 @>
        test <@ tracedDefect |> Flow.catch (fun error -> error.Message) |> Flow.orElseWith (fun error -> Flow.ok error.Length) |> Flow.runSync () = Exit.Success 6 @>
        test <@ traced |> Flow.tapError (fun error -> Flow.delay (fun () -> tapped.Value <- Some error; Flow.ok ())) |> Flow.runSync () |> Exit.map ignore <> Exit.Success () && tapped.Value = Some "boom" @>
        test <@ retried |> Flow.runSync () = Exit.Success 3 @>
        test <@ Cause.untraced (Cause.traced "a" (Cause.traced "b" (Cause.Fail 1))) = Cause.Fail 1 @>

    [<Fact>]
    let ``A flow that cannot fail widens to any error type and keeps defects`` () =
        let succeeds : Flow<unit, string, int> = Flow.ok 1 |> Flow.widenError
        let dies : Flow<unit, string, int> = Flow.die (InvalidOperationException "defect") |> Flow.widenError

        test <@ succeeds |> Flow.orElse (Flow.ok 0) |> Flow.runSync () = Exit.Success 1 @>
        test <@ match Flow.runSync () dies with Exit.Failure(Cause.Die error) -> error.Message = "defect" | _ -> false @>
        test <@ Exit.Success 2 |> Exit.mapError Never.absurd = (Exit.Success 2 : Exit<int, string>) @>

