// axial-allow-file-AXG001: executable AOT probe performs console, process, and blocking host-boundary effects
/// Runs under NativeAOT and fails loudly when a code path depends on metadata that AOT removes: F#'s reflection-based
/// structural rendering, diagnostics rendering, and deep
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
        | Rejected code -> "Rejected " + code.ToString()

/// A user error type without its own rendering: its generated ToString needs reflection that AOT removes.
type PlainError = Refused of int

let mutable failures = 0

let expect (name: string) (actual: string) (expected: string) =
    if actual <> expected then
        failures <- failures + 1
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Error.WriteLine(String.Concat("FAIL ", name, ": expected <", expected, "> got <", actual, ">"))
    else
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Out.WriteLine("ok   " + name)

let expectContains (name: string) (actual: string) (fragment: string) =
    if not (actual.Contains fragment) then
        failures <- failures + 1
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Error.WriteLine(String.Concat("FAIL ", name, ": <", actual, "> does not contain <", fragment, ">"))
    else
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Out.WriteLine("ok   " + name)

let guarded (name: string) (check: unit -> unit) =
    try
        check ()
    with error ->
        failures <- failures + 1
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Error.WriteLine(String.Concat("FAIL ", name, " threw ", error.GetType().FullName, ": ", error.Message))

[<EntryPoint>]
let main _ =
    guarded "flow result" (fun () ->
        let exit =
            // executable host boundary performs this effect.
            // axial-allow-AXG001: intentional blocking host or live-service boundary
            (Flow.succeed 21 |> Flow.map ((*) 2)).StartAsTask(()).GetAwaiter().GetResult()

        expect "flow result" (exit.ToString()) "Success(42)")

    guarded "outcome rendering" (fun () ->
        let failed: Exit<int, ProbeError> =
            Exit.Failure(
                Cause.Then(
                    Cause.Fail(Rejected 7),
                    Cause.Both(Cause.Interrupt, Cause.Traced(Cause.Die(InvalidOperationException "boom"), "trace"))
                )
            )

        expect
            "Exit.ToString"
            (failed.ToString())
            "Failure(Then(Fail(Rejected 7), Both(Interrupt, Traced(Die(InvalidOperationException: boom), trace))))"

        expectContains
            "Cause.prettyPrint"
            (Cause.prettyPrint
                (fun (error: ProbeError) -> error.ToString())
                (Cause.Both(Cause.Fail(Rejected 1), Cause.Interrupt)))
            "Fail(Rejected 1)"

        expect "Exit string payload" ((Exit<string, ProbeError>.Success "x").ToString()) "Success(\"x\")"
        let plain: Exit<int, PlainError> = Exit.Failure(Cause.Fail(Refused 3))
        let rendered = plain.ToString()
        // With --reflectionfree, a union without an override falls back to its runtime type name.
        if not (rendered.StartsWith("Failure(Fail(") && rendered.EndsWith("PlainError))")) then
            failures <- failures + 1
            // Probe reporting is an executable host-boundary console effect.
            // axial-allow-AXG001: intentional console host or live-service boundary
            Console.Error.WriteLine("FAIL Exit.ToString without own ToString: got <" + rendered + ">")
        else
            // Probe reporting is an executable host-boundary console effect.
            // axial-allow-AXG001: intentional console host or live-service boundary
            Console.Out.WriteLine("ok   Exit.ToString without own ToString"))

    guarded "Exit.toResult composite" (fun () ->
        try
            Exit.toResult (Exit<int, ProbeError>.Failure(Cause.Then(Cause.Fail(Rejected 1), Cause.Fail(Rejected 2))))
            |> ignore

            failures <- failures + 1
            // Probe reporting is an executable host-boundary console effect.
            // axial-allow-AXG001: intentional console host or live-service boundary
            Console.Error.WriteLine("FAIL Exit.toResult composite: expected an exception")
        with :? InvalidOperationException as error ->
            expectContains "Exit.toResult composite" error.Message "Fail(Rejected 1)")

    guarded "fiber diagnostics" (fun () ->
        expect "FiberStatus" (FiberStatus.Interrupted.ToString()) "Interrupted"
        expect "FiberId" ((FiberId 5L).ToString()) "#5"
        let registry = FiberRegistry()

        let workflow: Flow<ClockEnvironment, ProbeError, string> =
            flow {
                let! clock = Flow.envWith (fun (env: ClockEnvironment) -> env.Clock)

                let! fiber =
                    Flow.forkNamed "probe child" (Flow.sleep (TimeSpan.FromMilliseconds 200.0))

                let dump = registry.DumpAt(clock)

                let snapshot =
                    registry.Snapshot()
                    |> List.map (fun dump -> dump.ToString())
                    |> String.concat "\n"

                do! Fiber.join fiber
                return dump + "\n" + snapshot
            }
            |> Flow.withFiberRegistry registry

        // executable host boundary performs this effect.

        // axial-allow-AXG001: intentional blocking host or live-service boundary
        match workflow.StartAsTask(ClockEnvironment(Clock.live)).GetAwaiter().GetResult() with
        | Exit.Success text -> expectContains "FiberRegistry.DumpAt" text "\"probe child\" Running"
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

        expect
            "200k synchronous loop"
            // executable host boundary performs this effect.
            // axial-allow-AXG001: intentional blocking host or live-service boundary
            ((workflow.StartAsTask(()).GetAwaiter().GetResult()).ToString())
            "Success(200000)")

    guarded "library type rendering" (fun () ->
        expect "LogLevel" (LogLevel.Warning.ToString()) "Warning"
        expect "StreamStep" ((StreamStep<int, ProbeError>.Done).ToString()) "Done"
        expect "AttributeValue" ((Axial.Telemetry.AttributeValue.IntegerValues [ 1L; 2L ]).ToString()) "[1; 2]"

        expect
            "ProcessError"
            ((ProcessError.StartFailed { Command = "git"; Message = "missing" }).ToString())
            "Could not start 'git': missing"

        expect "HttpError" ((HttpError.InvalidRequest "bad").ToString()) "Invalid HTTP request: bad"
        expect "FileSystemError" ((FileSystemError.FileNotFound "/x").ToString()) "File not found: /x"

        expect
            "EnvironmentVariableError"
            ((EnvironmentVariableError.MissingVariable "HOME").ToString())
            "Missing required environment variable 'HOME'."

        expect
            "OutputTarget"
            ((OutputTarget.Tee [ OutputTarget.Discard; OutputTarget.File "log" ]).ToString())
            "Tee(Discard, File log)")

    guarded "process capture and streaming" (fun () ->
        let command = Process.commandArgs "sh" [ "-c"; "echo first; echo second" ]

        let checkResult (outcome: Exit<ProcessResult, ProcessError>) =
            match outcome with
            | Exit.Success result -> expect "process stdout" result.StdOut "first\nsecond\n"
            | Exit.Failure cause -> failwith (cause.ToString())

        Process.tryRun command |> checkResult

        command
        |> Process.pipe (Process.commandArgs "cat" [])
        |> Process.tryRun
        |> checkResult

        command |> Process.toFlow |> Process.tryRun |> checkResult

        let completion =
            command
            |> Process.streamStdoutLines
            |> ProcessStream.runCollect
            |> Process.tryRun

        match completion with
        | Exit.Success([ "first"; "second" ], ProcessCompletion.Completed result) ->
            expect "stream exit" (string result.ExitCode) "0"
        | _ -> failwith "Process stream did not complete"

        let result = Process.commandArgs "true" [] |> Process.run
        expect "value runner" (string result.ExitCode) "0")

    if failures = 0 then
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Out.WriteLine("AOT probe passed.")
        0
    else
        // Probe reporting is an executable host-boundary console effect.
        // axial-allow-AXG001: intentional console host or live-service boundary
        Console.Error.WriteLine("AOT probe: " + string failures + " failure(s).")
        1
