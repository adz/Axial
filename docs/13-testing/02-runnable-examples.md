---
title: Runnable Examples
description: Executable Axial examples mirrored into the documentation.
---

# Runnable Examples

These pages transclude source from the example projects with snippet directives marked `no-check`: the docs test
does not compile or run them, because each example project builds and runs as its own program (CI runs the
Playground, MaintenanceExamples, and Examples projects). The literal `Observed output` text blocks beside them are
maintained manually from those runs, not verified by FsLiveDocs, so keep them in sync when an example changes.
Checked `fsharp run` blocks elsewhere in the docs are the opposite: FsLiveDocs compiles and executes them and fails
on a mismatch.

The `dotnet run` commands below are repository-relative: run them from the repository root with
the example projects checked out next to `src/`. The example projects are not shipped in the NuGet
packages, so these paths do not exist in an application that only references Axial.

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
        |> Flow.run env

    let asyncResult =
        greetingAsync
        |> Flow.run env

    let taskResult =
        greetingTask
        |> Flow.run env

    printfn "Flow: %A" syncResult
    printfn "Async: %A" asyncResult
    printfn "Task: %A" taskResult
    // Flow: Success "Hello Ada"
    // Async: Success "HELLO ADA"
    // Task: Success "Hello Ada!"
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

This project runs several examples in one process: the request-boundary example, the policy
examples, and then the supervision example below. The output before the
`=== Supervision and fiber observability ===` banner belongs to those earlier examples and is omitted here. To run
only the supervision example, set the `AXIAL_EXAMPLE` environment variable to `supervision`.

Source: [SupervisionExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/SupervisionExample.fs)

{{< snippet id="supervision-example" mode="no-check" reason="Compiled and run as its own example project" >}}

Observed output (supervision portion):

```text
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
