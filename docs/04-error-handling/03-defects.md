---
title: Defects and Exceptions
description: Why Axial separates domain failures, interruptions, and defects.
---

# Defects and Exceptions

Axial distinguishes expected failures, interruption, and unexpected defects. Domain failures stay in the typed error channel. Defects are recorded in the execution outcome so cleanup and observers can see them.

## Quick Start: Usage Patterns

### Producing Failures
Choose the function that matches your intent:

| Intent | Function | Outcome |
| :--- | :--- | :--- |
| **Domain Error** (Expected) | `Flow.fail "Not found"` | `Cause.Fail "Not found"` |
| **Defect/Panic** (Bug) | `Flow.die (exn "Database down")` | `Cause.Die exn` |
| **Interruption** | `Fiber.interrupt` or runtime cancellation | `Cause.Interrupt` |
| **Sequential Failures** | Workflow fails, then cleanup fails | `Cause.Then (workflowCause, cleanupCause)` |
| **Parallel Failures** | Parallel branches both fail | `Cause.Both (leftCause, rightCause)` |

### Bridging Exceptions
Use `Flow.attemptAsync`, `Flow.attemptTask`, or `Flow.attemptValueTask` when exceptions from an interop boundary are expected and should enter the typed error channel. These constructors return `Cause.Fail exn` for non-cancellation exceptions and `Cause.Interrupt` for cancellation.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let loadConfig : ExnFlow<string> =
    Flow.attemptTask (fun token -> File.ReadAllTextAsync("appsettings.json", token))
```

Use `Flow.catch` to convert simple defects into domain errors after a flow has already produced `Cause.Die`. Existing typed failures and interruptions are preserved. Compound causes such as `Cause.Then` and `Cause.Both` are left unchanged.

```fsharp
type ParseError = InvalidNumber of string

let parseCount (text: string) : Flow<ParseError, int> =
    Flow.delay (fun () -> Flow.ok (Int32.Parse text))
    |> Flow.catch (function
        | :? FormatException -> InvalidNumber text
        | error -> raise error)
```

`Int32.Parse` throws, so the flow first ends with `Cause.Die`; `Flow.catch` then turns a `FormatException` into the
typed error:

```fsharp run
parseCount "42" |> Flow.run () |> shouldEqual (Exit.Success 42)
parseCount "forty" |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail(InvalidNumber "forty")))
```

---

## Rationale

Axial records defects in the `Exit` type for three reasons.

### 1. One Outcome Shape
In complex orchestration like `Flow.zipPar` (running two flows concurrently), the engine must coordinate the lifecycle of multiple [**fibers**](/concurrency-and-state/fibers.html).

*   **Problem:** If a defect is only a thrown exception, it escapes the return value. The engine has to handle two failure paths: returned failures and thrown exceptions.
*   **Approach:** By capturing defects into the `Exit` type, every flow execution returns a value. If one branch dies, the engine receives it as data, can interrupt the other branches, and returns one structured outcome.

### 2. Concurrency Coordination
When a fiber fails, you often need to perform cleanup (e.g., `ensuring` or `onExit`). 

By recording defects as `Cause.Die`, Axial passes the original exception and stack trace to finalizers as a value. Finalizers can log why a background fiber died without adding `try...with` blocks around every cleanup action.

If cleanup itself fails after the workflow has already failed, Axial does not discard either side. It returns `Cause.Then (workflowCause, cleanupCause)` so observability and host boundaries can see the original failure and the cleanup defect in order.

### 3. Precision in Retries and Fallbacks
The distinction between `Fail` and `Die` gives retry and fallback code a clear default:
*   **Retries** should usually target `Fail` (e.g., a transient network error), but never `Die` (e.g., a `NullReferenceException`). Retrying a bug is usually a waste of resources.
*   **Fallbacks** (`orElse`) usually target domain failures. If a workflow has a defect, it usually indicates a corrupted state that fallback logic wasn't designed to handle.
