/// A minimal test harness for Fable: xUnit does not run under JavaScript, so each test is a Flow that returns its
/// checks, and the runner reports them and sets the process exit code.
module Axial.Fable.Tests.Harness

open System
open Axial
open Fable.Core

/// One named expectation inside a test.
type Check = { Label: string; Passed: bool; Detail: string }

/// A named test: a flow that returns its checks. A typed failure, defect, or interruption fails the test.
type Test = { Name: string; Body: Flow<unit, Never, Check list> }

let equal (label: string) (expected: 'value) (actual: 'value) : Check =
    let passed = (actual = expected)

    { Label = label
      Passed = passed
      // Rendered only on failure: structured formatting of large values is slow under JavaScript.
      // Fable renders %A with its own JavaScript formatter; this project is never compiled trimmed or AOT.
      // axial-allow-reflection-format
      Detail = if passed then "" else $"expected %A{expected}, got %A{actual}" }

let isTrue (label: string) (condition: bool) : Check =
    { Label = label
      Passed = condition
      Detail = "expected true" }

let test (name: string) (body: Flow<unit, Never, Check list>) : Test = { Name = name; Body = body }

/// Runs a flow and returns its outcome as a value, so a test can check failures and interruptions.
let exitOf (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
    flow |> Flow.fold (fun value -> Flow.ok (Exit.Success value)) (fun cause -> Flow.ok (Exit.Failure cause))

/// Milliseconds on the JavaScript monotonic clock, for coarse timing checks.
[<Emit("performance.now()")>]
let nowMilliseconds () : float = jsNative

[<Emit("process.argv.slice(2)")>]
let private arguments () : string array = jsNative

[<Emit("process.exitCode = $0")>]
let private setExitCode (_code: int) : unit = jsNative

// Each test must finish within this bound; a hang is reported as a failure instead of stalling the run.
let private testTimeout = TimeSpan.FromSeconds 20.0

let private timedOut : Check list = [ { Label = "finished in time"; Passed = false; Detail = "the test hung" } ]

/// Runs every test in order, prints one line per test and per failed check, and sets a non-zero exit code on failure.
/// A command-line argument runs only the tests whose name contains it.
let run (allTests: Test list) : unit =
    let tests =
        match arguments () with
        | [| filter |] -> allTests |> List.filter (fun candidate -> candidate.Name.Contains filter)
        | _ -> allTests

    async {
        let failures = ref 0

        for current in tests do
            let! exit = current.Body |> Flow.timeoutToOk testTimeout timedOut |> Flow.toAsync ()

            match exit with
            | Exit.Success checks when checks |> List.forall _.Passed -> printfn "  pass %s" current.Name
            | Exit.Success checks ->
                failures.Value <- failures.Value + 1
                printfn "  FAIL %s" current.Name

                for check in checks |> List.filter (fun check -> not check.Passed) do
                    printfn "       %s: %s" check.Label check.Detail
            | Exit.Failure cause ->
                failures.Value <- failures.Value + 1
                printfn "  FAIL %s" current.Name
                printfn "       ended with %s" (Cause.prettyPrint (fun (_: Never) -> "") cause)

        printfn "%d tests, %d failed" tests.Length failures.Value
        setExitCode (if failures.Value = 0 then 0 else 1)
    }
    |> Async.StartImmediate
