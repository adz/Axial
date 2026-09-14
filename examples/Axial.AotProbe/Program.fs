/// Runs under NativeAOT and fails loudly when a code path depends on metadata that AOT removes: F#'s reflection-based
/// structured printing (compiler-generated ToString on unions and records, %A), diagnostics rendering, and deep
/// synchronous flow chains. `scripts/run-aot-probe.sh` publishes and runs it.
open System
open Axial
open Axial.PlatformService
open Axial.Process
open Axial.FileSystem
open Axial.HttpClient

/// A user error type with its own rendering, as AOT-safe applications write them.
type ProbeError =
    | Rejected of int
    override this.ToString() =
        match this with
        | Rejected code -> $"Rejected {code}"

let mutable failures = 0

let expect (name: string) (actual: string) (expected: string) =
    if actual <> expected then
        failures <- failures + 1
        eprintfn "FAIL %s: expected <%s> got <%s>" name expected actual
    else
        printfn "ok   %s" name

let expectContains (name: string) (actual: string) (fragment: string) =
    if not (actual.Contains fragment) then
        failures <- failures + 1
        eprintfn "FAIL %s: <%s> does not contain <%s>" name actual fragment
    else
        printfn "ok   %s" name

let guarded (name: string) (check: unit -> unit) =
    try
        check ()
    with error ->
        failures <- failures + 1
        eprintfn "FAIL %s threw %s: %s" name (error.GetType().FullName) error.Message

[<EntryPoint>]
let main _ =
    guarded "flow result" (fun () ->
        let exit = (Flow.succeed 21 |> Flow.map ((*) 2)).StartAsTask(()).GetAwaiter().GetResult()
        expect "flow result" (exit.ToString()) "Success(42)")

    guarded "outcome rendering" (fun () ->
        let failed: Exit<int, ProbeError> = Exit.Failure(Cause.Then(Cause.Fail(Rejected 7), Cause.Both(Cause.Interrupt, Cause.Traced(Cause.Die(InvalidOperationException "boom"), "trace"))))
        expect "Exit.ToString" (failed.ToString()) "Failure(Then(Fail(Rejected 7), Both(Interrupt, Traced(Die(InvalidOperationException: boom), trace))))"
        expectContains "Cause.prettyPrint" (Cause.prettyPrint (fun (error: ProbeError) -> error.ToString()) (Cause.Both(Cause.Fail(Rejected 1), Cause.Interrupt))) "Fail(Rejected 1)"
        expect "Exit string payload" ((Exit<string, ProbeError>.Success "x").ToString()) "Success(\"x\")")

    guarded "Exit.toResult composite" (fun () ->
        try
            Exit.toResult (Exit<int, ProbeError>.Failure(Cause.Then(Cause.Fail(Rejected 1), Cause.Fail(Rejected 2)))) |> ignore
            failures <- failures + 1
            eprintfn "FAIL Exit.toResult composite: expected an exception"
        with :? InvalidOperationException as error ->
            expectContains "Exit.toResult composite" error.Message "Fail(Rejected 1)")

    guarded "fiber diagnostics" (fun () ->
        expect "FiberStatus" (FiberStatus.Interrupted.ToString()) "Interrupted"
        expect "FiberId" ((FiberId 5L).ToString()) "#5"
        let registry = FiberRegistry()
        let workflow: Flow<unit, ProbeError, string> =
            flow {
                let! fiber = Flow.forkNamed "probe child" (Flow.Runtime.sleep (TimeSpan.FromMilliseconds 200.0))
                let dump = registry.DumpAt(DateTimeOffset.UtcNow)
                let snapshot = registry.Snapshot() |> List.map (fun dump -> dump.ToString()) |> String.concat "\n"
                do! Flow.join fiber
                return dump + "\n" + snapshot
            }
            |> Flow.withFiberRegistry registry
        match workflow.StartAsTask(()).GetAwaiter().GetResult() with
        | Exit.Success text ->
            expectContains "FiberRegistry.DumpAt" text "\"probe child\" Running"
        | other -> expect "fiber diagnostics exit" (other.ToString()) "Success")

    guarded "stack safety" (fun () ->
        let workflow: Flow<unit, ProbeError, int> =
            flow {
                let mutable count = 0
                while count < 200_000 do
                    do! Flow.succeed ()
                    count <- count + 1
                return count
            }
        expect "200k synchronous loop" ((workflow.StartAsTask(()).GetAwaiter().GetResult()).ToString()) "Success(200000)")

    guarded "library type rendering" (fun () ->
        expect "LogLevel" (LogLevel.Warning.ToString()) "Warning"
        expect "StreamStep" ((StreamStep<int, ProbeError>.Done).ToString()) "Done"
        expect "AttributeValue" ((Axial.Telemetry.AttributeValue.IntegerValues [ 1L; 2L ]).ToString()) "[1; 2]"
        expect "ProcessError" ((ProcessError.StartFailed { Command = "git"; Message = "missing" }).ToString()) "Could not start 'git': missing"
        expect "HttpError" ((HttpError.InvalidRequest "bad").ToString()) "Invalid HTTP request: bad"
        expect "FileSystemError" ((FileSystemError.FileNotFound "/x").ToString()) "File not found: /x"
        expect "EnvironmentVariableError" ((EnvironmentVariableError.MissingVariable "HOME").ToString()) "Missing required environment variable 'HOME'."
        expect "OutputTarget" ((OutputTarget.Tee [ OutputTarget.Capture; OutputTarget.File "log" ]).ToString()) "Tee(Capture, File log)")

    if failures = 0 then
        printfn "AOT probe passed."
        0
    else
        eprintfn "AOT probe: %d failure(s)." failures
        1
