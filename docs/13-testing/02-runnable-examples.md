---
title: Runnable Examples
description: Executable Axial examples mirrored into the documentation.
---

# Runnable Examples

These examples are built and run while this page is generated, keeping the documentation tied to executable code.

## Playground

Run it:

```bash
dotnet run --project examples/Axial.Playground/Axial.Playground.fsproj --nologo
```

Source: [Program.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Playground/Program.fs)

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open Axial

type AppEnv =
    { Prefix: string
      Name: string
      LoadSuffix: Task<string> }

let greetingFlow : Flow<AppEnv, string, string> =
    Flow.envWith (fun env -> $"{env.Prefix} {env.Name}") // Flow<AppEnv, string, string>

let greetingAsync : Flow<AppEnv, string, string> =
    flow {
        let! greeting = greetingFlow
        let! (checkedGreeting: string) =
            if String.IsNullOrWhiteSpace greeting then
                Error "Blank greeting"
            else
                Ok greeting

        return checkedGreeting.ToUpperInvariant()
    }

let greetingTask : Flow<AppEnv, string, string> =
    flow {
        let! env = Flow.env // Flow<AppEnv, string, AppEnv>
        let! greeting = greetingFlow // Flow<AppEnv, string, string>
        let! suffix = Flow.awaitStartedTask env.LoadSuffix
        return $"{greeting}{suffix}"
    }

[<EntryPoint>]
let main _ =
    let env =
        { Prefix = "Hello"
          Name = "Ada"
          LoadSuffix = Task.FromResult "!" }

    let syncResult =
        greetingFlow
        |> fun workflow -> workflow |> Flow.run env

    let asyncResult =
        greetingAsync
        |> fun workflow -> workflow |> Flow.run env

    let taskResult =
        greetingTask
        |> fun workflow -> workflow |> Flow.run env

    printfn "Flow: %A" syncResult
    printfn "Async: %A" asyncResult
    printfn "Task: %A" taskResult
    // Flow: Ok "Hello Ada"
    // Async: Ok "HELLO ADA"
    // Task: Ok "Hello Ada!"
    0

```

Observed output:

```text
Flow: Success "Hello Ada"
Async: Success "HELLO ADA"
Task: Success "Hello Ada!"
```

## Maintenance patterns

Run it:

```bash
dotnet run --project examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj --nologo
```

Source: [Program.fs](https://github.com/adz/Axial/blob/main/examples/Axial.MaintenanceExamples/Program.fs)

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
open System
open System.Threading
open System.Threading.Tasks
open Axial

let runFlow label env (workflow: Flow<'env, 'error, 'value>) =
    let result = workflow |> Flow.run env
    printfn "%s: %A" label result

let runAsyncExample label env (workflow: Flow<'env, 'error, 'value>) =
    let result =
        workflow
        |> fun workflow -> workflow |> Flow.run env

    printfn "%s: %A" label result

let runTaskExample label env (workflow: Flow<'env, 'error, 'value>) =
    let result =
        workflow
        |> fun workflow -> workflow |> Flow.run env

    printfn "%s: %A" label result

let syncExample : Flow<int, string, int> =
    Flow.envWith id // Flow<int, string, int>
    |> Flow.map ((+) 1)

let asyncExample : Flow<int, string, int> =
    flow {
        let! value = async { return 21 }
        return value * 2
    }

let taskExample : Flow<int, string, int> =
    flow {
        let! env = Flow.envWith id
        let! suffix = ColdTask(fun _ -> Task.FromResult 5)
        return env + suffix
    }

[<EntryPoint>]
let main _ =
    runFlow "Flow" 20 syncExample
    runAsyncExample "Async" 20 asyncExample
    runTaskExample "Task" 20 taskExample
    // Flow: Ok 21
    // Async: Ok 42
    // Task: Ok 25
    0

```

Observed output:

```text
Flow: Success 21
Async: Success 42
Task: Success 25
```

## Supervision and fiber observability

Run it:

```bash
dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source: [SupervisionExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/SupervisionExample.fs)

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
module SupervisionExample

open System
open Axial

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
        |> Flow.run ()

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

    let result = workflow |> Flow.run ()
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

    let result = workflow |> Flow.run ()
    printfn $"  result: %A{result}"

let run () =
    printfn "=== Supervision and fiber observability ==="
    supervisedRecovery ()
    unobservedDefectReporting ()
    intentionalFireAndForget ()

```

Observed output:

```text
Flow result: Success { Id = 42
          Name = "Ada" }
Flow result: Success "Hello [11111111-1111-1111-1111-111111111111] Ada"
Flow result: Success "Hello [11111111-1111-1111-1111-111111111111] Ada!"

Policy examples
  accepted:            Success { Sku = "SKU-1"
          Quantity = 3 }
  rejected (not int):  Failure (Fail QuantityNotANumber)
  rejected (zero):     Failure (Fail QuantityNotPositive)
  rejected (over cap): Failure (Fail (QuantityOverCap 10))
  cap disabled:        Success { Sku = "SKU-1"
          Quantity = 50 }

=== Supervision and fiber observability ===
-- Flow.Runtime.supervise: restart a background worker that dies with a defect
  result after 3 attempts: Success "worker succeeded on attempt 3"
-- FiberObserver: a discarded fork handle whose fiber dies is reported
  [observer] fiber N died: background job blew up
  [observer] UNOBSERVED DEFECT from fiber N: background job blew up
  result: Success "main workflow finished fine"
-- Flow.forkDetached: intentional fire-and-forget is not reported as unobserved
  [observer] fiber N died: best-effort work failed
  result: Success "no unobserved-defect report for detached work"
```

