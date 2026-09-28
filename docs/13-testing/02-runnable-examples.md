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

{{< snippet id="maintenance-examples" mode="no-check" reason="Compiled and run as its own example project" >}}

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

{{< snippet id="supervision-example" mode="no-check" reason="Compiled and run as its own example project" >}}

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
-- Flow.supervise: restart a background worker that dies with a defect
  result after 3 attempts: Success "worker succeeded on attempt 3"
-- FiberObserver: a discarded fork handle whose fiber dies is reported
  [observer] fiber N died: background job blew up
  [observer] UNOBSERVED DEFECT from fiber N: background job blew up
  result: Success "main workflow finished fine"
-- Flow.forkDetached: intentional fire-and-forget is not reported as unobserved
  [observer] fiber N died: best-effort work failed
  result: Success "no unobserved-defect report for detached work"
```

## Concurrency torture tests

Run every scenario for 10 rounds, or name a scenario and a round count:

```bash
dotnet run --project examples/Axial.TortureTest
dotnet run --project examples/Axial.TortureTest -- queues 200
```

Source: [examples/Axial.TortureTest](https://github.com/adz/Axial/tree/main/examples/Axial.TortureTest)

Sixteen scenarios drive queues, hubs, semaphores, deferreds, refs, STM, fibers, caches, schedules, parallel
combinators, errors, scopes, layers, streams, and Task and Async interop under random interruption, failure, and
shutdown. The program reports each invariant it checks and exits with a non-zero code if any was violated. The
[torture tests](/concurrency-and-state/torture-tests/index.html) section walks through each scenario and what each
check proves.
