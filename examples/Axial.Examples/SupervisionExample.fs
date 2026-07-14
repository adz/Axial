module SupervisionExample

open System
open Axial.Flow

// Demonstrates defect supervision and fiber observability:
// 1. Flow.Runtime.supervise restarts background work that dies with a defect.
// 2. A FiberObserver installed once at the edge reports defects from fibers nobody awaited.
// 3. Flow.forkDetached states intentional fire-and-forget at the call site, silencing the report.

let private flakyWorker (attempts: int ref) : Flow<unit, string, string> =
    Flow.delay(fun () ->
        attempts.Value <- attempts.Value + 1

        if attempts.Value < 3 then
            // A bug, not a typed domain error: supervise restarts these.
            Flow.die (InvalidOperationException $"worker crashed on attempt {attempts.Value}")
        else
            Flow.succeed $"worker succeeded on attempt {attempts.Value}")

let private consoleObserver =
    { FiberObserver.none with
        OnEnd = fun metadata defect ->
            match defect with
            | Some exn -> printfn $"  [observer] fiber {metadata.Id.Value} died: {exn.Message}"
            | None -> printfn $"  [observer] fiber {metadata.Id.Value} ended: {metadata.Status}"
        OnUnobservedDefect = fun metadata defect ->
            let source =
                match metadata with
                | Some m -> $"fiber {m.Id.Value}"
                | None -> "race/timeout loser"

            printfn $"  [observer] UNOBSERVED DEFECT from {source}: {defect.Message}" }

let private supervisedRecovery () =
    printfn "-- Flow.Runtime.supervise: restart a background worker that dies with a defect"
    let attempts = ref 0

    let policy : SupervisePolicy =
        { MaxAttempts = 5
          Delay = fun _ -> TimeSpan.Zero
          ShouldRestart = fun _ -> true }

    let result =
        flakyWorker attempts
        |> Flow.Runtime.supervise policy
        |> fun workflow -> workflow.RunSynchronously(())

    printfn $"  result after {attempts.Value} attempts: %A{result}"

let private unobservedDefectReporting () =
    printfn "-- FiberObserver: a discarded fork handle whose fiber dies is reported"

    let workflow =
        flow {
            // The handle is deliberately discarded: without an observer this crash is silent.
            let! _fiber = Flow.fork (Flow.die (InvalidOperationException "background job blew up") : Flow<unit, string, int>)
            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            return "main workflow finished fine"
        }
        |> Flow.withFiberObserver consoleObserver

    let result = workflow.RunSynchronously(())
    printfn $"  result: %A{result}"

let private intentionalFireAndForget () =
    printfn "-- Flow.forkDetached: intentional fire-and-forget is not reported as unobserved"

    let workflow =
        flow {
            let! _fiber = Flow.forkDetached (Flow.die (InvalidOperationException "best-effort work failed") : Flow<unit, string, int>)
            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            return "no unobserved-defect report for detached work"
        }
        |> Flow.withFiberObserver consoleObserver

    let result = workflow.RunSynchronously(())
    printfn $"  result: %A{result}"

let run () =
    printfn "=== Supervision and fiber observability ==="
    supervisedRecovery ()
    unobservedDefectReporting ()
    intentionalFireAndForget ()
