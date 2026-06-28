namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowErrorTests =
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
            |> Flow.orElse (Flow.read (fun env -> env + 1))
            |> Flow.runSync 41

        let bypassesFallback =
            Flow.succeed 10
            |> Flow.orElse (Flow.succeed 99)
            |> Flow.runSync ()

        let zipped =
            Flow.zip (Flow.read (fun env -> env + 1)) (Flow.read (fun env -> env * 2))
            |> Flow.runSync 5

        let mapped =
            Flow.map2 (+) (Flow.read (fun env -> env + 1)) (Flow.read (fun env -> env * 2))
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
            |> Flow.fromTask
            |> Flow.runSync ()

        let attemptTaskResult =
            Task.FromException<int>(taskDefect)
            |> Flow.attemptTask
            |> Flow.runSync ()

        let attemptValueTaskResult =
            ValueTask<int>(Task.FromException<int>(valueTaskDefect))
            |> Flow.attemptValueTask
            |> Flow.runSync ()

        let attemptAsyncResult =
            async { return raise asyncDefect }
            |> Flow.attemptAsync
            |> Flow.runSync ()

        let attemptCancellationResult =
            Task.FromCanceled<int>(CancellationToken(true))
            |> Flow.attemptTask
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

        test <@ attemptCancellationResult = Exit.Failure Cause.Interrupt @>

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
                let! value = async { return raise defect }
                return value
            }
            |> Flow.runSync ()

        let asyncCanceled =
            flow {
                let! value = async { return raise canceled }
                return value
            }
            |> Flow.runSync ()

        let taskDefect =
            flow {
                let! value = Task.FromException<int>(defect)
                return value
            }
            |> Flow.runSync ()

        let taskCanceled =
            flow {
                let! value = Task.FromCanceled<int>(CancellationToken(true))
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

        test <@ asyncCanceled = Exit.Failure Cause.Interrupt @>

        match taskDefect with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other

        test <@ taskCanceled = Exit.Failure Cause.Interrupt @>

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
                let! value = async { return raise defect }
                return value
            }
            |> Flow.runSync ()

        let taskBoundaryResult =
            flow {
                let! value = Task.FromException<int>(defect)
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
                workflow |> Schedule.retry (Schedule.recurs 5)

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
    let ``Check bridges into flow shapes`` () =
        let flowBridge =
            Result.require false ()
            |> Flow.orElseFlow (Flow.read (fun env -> $"flow:{env}"))
            |> Flow.runSync "env"

        let flowValue = Flow.value "flow-value" |> Flow.runSync ()

        test <@ flowBridge = Exit.Failure (Cause.Fail "flow:env") @>
        test <@ flowValue = Exit.Success "flow-value" @>

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

        let flowArgumentTypeNames = flowBuilderBindAndReturnFromArgumentNames ()

        test <@ Flow.runSync 20 syncSome = Exit.Success 42 @>
        test <@ Flow.runSync 20 syncNone = Exit.Failure (Cause.Fail ()) @>
        test <@ Flow.runSync 20 syncValueSome = Exit.Success 42 @>
        test <@ Flow.runSync 20 syncValueNone = Exit.Failure (Cause.Fail ()) @>
        test <@ Flow.runSync 19 asyncWorkflow = Exit.Success 42 @>
        test <@ Flow.runSync () asyncReturnFromNone = Exit.Failure (Cause.Fail ()) @>
        test <@ Flow.runSync 19 taskWorkflow = Exit.Success 42 @>
        test <@ Flow.runSync () taskReturnFromValueNone = Exit.Failure (Cause.Fail ()) @>
        test <@ flowArgumentTypeNames |> Array.contains "FSharpOption`1" @>
        test <@ flowArgumentTypeNames |> Array.contains "FSharpResult`2" @>
        test <@ flowArgumentTypeNames |> Array.contains "FSharpValueOption`1" @>

    [<Fact>]
    let ``option and valueoption implicit binding requires unit workflow errors`` () =
        let flowAssemblyPath = typeof<FlowBuilder>.Assembly.Location
        let resultAssemblyPath = typeof<ResultBuilder>.Assembly.Location
        let validationAssemblyPath = typeof<Validation<unit, unit>>.Assembly.Location

        let flowProbe =
            $"""
#r @"{flowAssemblyPath}"
#r @"{resultAssemblyPath}"
#r @"{validationAssemblyPath}"
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

let probe : Flow<unit, string, int> =
    flow {{
        let! value = Some 42
        return value
    }}
"""

        let asyncProbe =
            $"""
#r @"{flowAssemblyPath}"
#r @"{resultAssemblyPath}"
#r @"{validationAssemblyPath}"
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

let probe : Flow<unit, string, int> =
    flow {{
        let! value = ValueSome 42
        return value
    }}
"""

        let flowExitCode, flowOutput = runFsiScript flowProbe
        let asyncExitCode, asyncOutput = runFsiScript asyncProbe

        test <@ flowExitCode <> 0 @>
        test <@ flowOutput.Contains("Flow<unit,unit,int>") @>
        test <@ asyncExitCode <> 0 @>
        test <@ asyncOutput.Contains("Flow<unit,unit,int>") @>

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
                do! Result.require true () |> Bind.error "check-failed"
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
                    Result.require (isPwdValid password user) ()
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
        let vt = badFlow.ToValueTask(())
        let exit = vt.GetAwaiter().GetResult()
        match exit with
        | Exit.Failure (Cause.Die ex) -> test <@ obj.ReferenceEquals(ex, defect) @>
        | other -> failwithf "Expected defect cause, got %A" other
