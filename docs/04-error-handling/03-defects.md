---
title: Defects and Exceptions
description: Why Axial separates domain failures, interruptions, and defects.
---

# Defects and Exceptions

Axial distinguishes expected failures, interruption, and unexpected defects. Domain failures stay in the typed error channel. Defects are recorded in the execution outcome so cleanup and observers can see them.

## Usage patterns

### Producing Failures
Choose a function by the outcome it produces:

| Intent | Function | Outcome |
| :--- | :--- | :--- |
| **Domain Error** (Expected) | `Flow.fail "Not found"` | `Cause.Fail "Not found"` |
| **Defect/Panic** (Bug) | `Flow.die (exn "Database down")` | `Cause.Die exn` |
| **Interruption** | `Fiber.interrupt` or runtime cancellation | `Cause.Interrupt` |
| **Sequential Failures** | Workflow fails, then cleanup fails | `Cause.Then (workflowCause, cleanupCause)` |
| **Parallel Failures** | Parallel branches both fail | `Cause.Both (leftCause, rightCause)` |

### Bridging Exceptions
Use `Flow.attemptAsync`, `Flow.attemptTask`, or `Flow.attemptValueTask` when exceptions from an interop boundary are expected and should enter the typed error channel. These constructors return `Cause.Fail exn` for thrown exceptions — including an `OperationCanceledException` the operation raised for its own reasons. They return `Cause.Interrupt` only when the runtime's own cancellation token requested the cancellation.

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

This example calls `File.ReadAllTextAsync` directly only to show `attemptTask` capturing an interop exception. For
application code, prefer [`FileSystem.readAllText`](/services/filesystem.html), which makes file access an explicit
dependency and returns `Flow<'env, FileSystemError, string>` instead of throwing.

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

Axial records defects in `Exit` so orchestration and cleanup can handle them alongside typed failures.

### 1. One Outcome Shape
`Flow.zipPar` coordinates multiple [**fibers**](/concurrency-and-state/fibers.html). If a defect escapes as a thrown
exception, the engine must handle both returned failures and thrown exceptions. Capturing defects in `Exit` lets the
engine receive a branch's defect as data, interrupt the other branches, and return one structured outcome.

### 2. Concurrency Coordination
When a fiber fails, `ensuring` or `onExit` may need to clean up. `Cause.Die` passes the original exception and stack
trace to finalizers as a value. Finalizers can log why a background fiber died without adding `try...with` blocks around every cleanup action.

If cleanup fails after the workflow, Axial retains both failures. It returns `Cause.Then (workflowCause, cleanupCause)` so observability and host boundaries can see the original failure and the cleanup defect in order.

### 3. Precision in Retries and Fallbacks
`Fail` and `Die` call for different retry and fallback behavior:
*   **Retries** should usually target `Fail` (e.g., a transient network error), but never `Die` (e.g., a `NullReferenceException`). Retrying a bug is usually a waste of resources.
*   **Fallbacks** (`orElse`) usually target domain failures. If a workflow has a defect, it usually indicates a corrupted state that fallback logic wasn't designed to handle.
