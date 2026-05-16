namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
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
            Flow.Retry(
                Flow.delay (fun () ->
                retryAttempts.Value <- retryAttempts.Value + 1
                raise defect),
                Schedule.recurs 5)
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
            Check.okIf false
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
        let fsFlowAssemblyPath = typeof<FlowBuilder>.Assembly.Location

        let flowProbe =
            $"""
#r @"{fsFlowAssemblyPath}"
open FsFlow

let probe : Flow<unit, string, int> =
    flow {{
        let! value = Some 42
        return value
    }}
"""

        let asyncProbe =
            $"""
#r @"{fsFlowAssemblyPath}"
open FsFlow

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
    let ``Guard constructors work in all flow families`` () =
        let successOption : int option = Some 42
        let successValueOption : int voption = ValueSome 10
        let asyncOption : Async<int option> = async { return Some 42 }
        let asyncValueOption : Async<int voption> = async { return ValueSome 10 }
        let asyncBool : Async<bool> = async { return true }
        let successTaskOption : Task<int option> = Task.FromResult(Some 5)
        let successTaskValueOption : ValueTask<int voption> = ValueTask.FromResult(ValueSome 3)
        let guardedSuccessOption : Result<int, string> = Guard.Of("missing-option", successOption)
        let guardedSuccessValueOption : Result<int, string> = Guard.Of("missing-voption", successValueOption)
        let guardedBool : Result<unit, string> = Guard.Of("bool-false", true)
        let guardedAsyncOption : Async<Result<int, string>> = Guard.Of("missing-option", asyncOption)
        let guardedAsyncValueOption : Async<Result<int, string>> = Guard.Of("missing-voption", asyncValueOption)
        let guardedAsyncBool : Async<Result<unit, string>> = Guard.Of("bool-false", asyncBool)
        let guardedTaskOption : Task<Result<int, string>> = Guard.Of("task-missing", successTaskOption)
        let guardedTaskValueOption : ValueTask<Result<int, string>> = Guard.Of("vtask-missing", successTaskValueOption)

        let flowTest =
            flow {
                let! x = guardedSuccessOption
                let! y = guardedSuccessValueOption
                do! guardedBool
                return x + y
            }

        let asyncFlowTest =
            flow {
                let! (x : int) = guardedAsyncOption
                let! (y : int) = guardedAsyncValueOption
                do! guardedAsyncBool
                return x + y
            }

        let taskFlowTest =
            flow {
                let! x = guardedSuccessOption
                let! y = guardedSuccessValueOption
                do! guardedBool
                let! z = guardedTaskOption
                let! w = guardedTaskValueOption
                return x + y + z + w
            }

        let flowResult = Flow.runSync () flowTest
        let asyncFlowResult = Flow.runSync () asyncFlowTest
        let taskFlowResult = Flow.runSync () taskFlowTest

        test <@ flowResult = Exit.Success 52 @>
        test <@ asyncFlowResult = Exit.Success 52 @>
        test <@ taskFlowResult = Exit.Success 60 @>

    [<Fact>]
    let ``Flow async syntax uses Guard constructors and error mapping`` () =
        let tryGetUser username = async { return if username = "missing" then None else Some username }
        let isPwdValid password user = password = $"{user}-pwd"
        let authorize user = async { return if user = "blocked" then Error "denied" else Ok () }
        let createAuthToken user = if user = "expired" then Error "token-expired" else Ok $"token-{user}"

        let login username password =
            flow {
                let userResult : Async<Result<string, LoginError>> = Guard.Of(InvalidUser, tryGetUser username)
                let! (user : string) = userResult

                let passwordCheck : Result<unit, LoginError> = Guard.Of(InvalidPwd, isPwdValid password user)
                do! passwordCheck

                let authorizeResult : Async<Result<unit, LoginError>> = Guard.MapError(Unauthorized, authorize user)
                do! authorizeResult

                let tokenResult : Result<string, LoginError> = Guard.MapError(TokenErr, createAuthToken user)
                return! tokenResult
            }

        let success = Flow.runSync () (login "alice" "alice-pwd")
        let authFailure = Flow.runSync () (login "blocked" "blocked-pwd")
        let tokenFailure = Flow.runSync () (login "expired" "expired-pwd")

        test <@ success = Exit.Success "token-alice" @>
        test <@ authFailure = Exit.Failure (Cause.Fail (Unauthorized "denied")) @>
        test <@ tokenFailure = Exit.Failure (Cause.Fail (TokenErr "token-expired")) @>

    [<Fact>]
    let ``Guard mapError stays symmetric across flow families`` () =
        let asyncSource : Async<Result<int, string>> = async { return Error "async-source" }
        let taskSource : Task<Result<int, string>> = task { return Error "task-source" }
        let asyncSuccess : Async<Result<int, string>> = async { return Ok 1 }

        let asyncMapped =
            let mappedAsyncSource : Async<Result<int, string>> =
                Guard.MapError((fun error -> $"mapped-{error}"), asyncSource)

            flow {
                let! value = mappedAsyncSource
                return value + 1
            }

        let taskMapped =
            let mappedAsyncSuccess : Async<Result<int, string>> =
                Guard.MapError((fun error -> $"mapped-{error}"), asyncSuccess)

            let mappedTaskSource : Task<Result<int, string>> =
                Guard.MapError((fun error -> $"mapped-{error}"), taskSource)

            flow {
                let! (asyncValue : int) = mappedAsyncSuccess
                let! (taskValue : int) = mappedTaskSource
                return asyncValue + taskValue
            }

        test <@ Flow.runSync () asyncMapped = Exit.Failure (Cause.Fail "mapped-async-source") @>
        test <@ Flow.runSync () taskMapped = Exit.Failure (Cause.Fail "mapped-task-source") @>

    [<Fact>]
    let ``Guard.of fails correctly for check-like sources`` () =
        let missingOption : int option = None
        let guardedFlowFail : Result<int, string> = Guard.Of("failed", missingOption)
        let guardedAsyncFlowFail : Async<Result<int, string>> = Guard.Of("failed", async { return ValueNone })
        let guardedTaskFlowFail : Result<int, string> = Guard.Of("failed", missingOption)

        let flowFail = flow {
            let! (value : int) = guardedFlowFail
            return value
        }
        let asyncFlowFail = flow {
            let! (value : int) = guardedAsyncFlowFail
            return value
        }
        let taskFlowFail = flow {
            let! (value : int) = guardedTaskFlowFail
            return value
        }

        test <@ Flow.runSync () flowFail = Exit.Failure (Cause.Fail "failed") @>
        test <@ Flow.runSync () asyncFlowFail = Exit.Failure (Cause.Fail "failed") @>
        test <@ Flow.runSync () taskFlowFail = Exit.Failure (Cause.Fail "failed") @>
