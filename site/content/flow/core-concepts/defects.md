---
weight: 40
title: Defects and Exceptions
description: Why Axial separates domain failures, interruptions, and defects.
type: docs
---


Axial distinguishes expected failures, interruption, and unexpected defects. Domain failures stay in the typed error channel. Defects are recorded in the execution outcome so cleanup and observers can see them.

## Quick Start: Usage Patterns

### Producing Failures
Choose the function that matches your intent:

| Intent | Function | Outcome |
| :--- | :--- | :--- |
| **Domain Error** (Expected) | [`Flow.fail`]({{< relref "/flow/reference/flow/construction/m-flow-flow-fail.md" >}}) `"Not found"` | `Cause.Fail "Not found"` |
| **Defect/Panic** (Bug) | `Flow.die (exn "Database down")` | `Cause.Die exn` |
| **Interruption** | [`Flow.interrupt`]({{< relref "/flow/reference/flow/concurrency/m-flow-flow-interrupt.md" >}}) or runtime cancellation | `Cause.Interrupt` |
| **Sequential Failures** | Workflow fails, then cleanup fails | `Cause.Then (workflowCause, cleanupCause)` |
| **Parallel Failures** | Parallel branches both fail | `Cause.Both (leftCause, rightCause)` |

### Bridging Exceptions
Use [`Flow.attemptAsync`]({{< relref "/flow/reference/flow/construction/m-flow-flow-attemptasync.md" >}}), [`Flow.attemptTask`]({{< relref "/flow/reference/flow/construction/m-flow-flow-attempttask.md" >}}), or [`Flow.attemptValueTask`]({{< relref "/flow/reference/flow/construction/m-flow-flow-attemptvaluetask.md" >}}) when exceptions from an interop boundary are expected and should enter the typed error channel. These constructors return `Cause.Fail exn` for non-cancellation exceptions and `Cause.Interrupt` for cancellation.

```fsharp
let loadConfig : ExnFlow<string> =
    Flow.attemptTask (File.ReadAllTextAsync("appsettings.json"))
```

Use [`Flow.catch`]({{< relref "/flow/reference/flow/composition/m-flow-flow-catch.md" >}}) to convert simple defects into domain errors after a flow has already produced `Cause.Die`. Existing typed failures and interruptions are preserved. Compound causes such as `Cause.Then` and `Cause.Both` are left unchanged.

```fsharp
let safeParse id =
    flow {
        let! json = Http.get id
        return Json.parse json
    }
    |> Flow.catch (function
        | :? JsonException as ex -> DomainError.InvalidFormat ex.Message
        | ex -> raise ex)
```

---

## Rationale

Axial records defects in the [`Exit`]({{< relref "/flow/reference/exit/_index.md" >}}) type for three reasons.

### 1. One Outcome Shape
In complex orchestration like [`Flow.zipPar`]({{< relref "/flow/reference/flow/concurrency/m-flow-flow-zippar.md" >}}) (running two flows concurrently), the engine must coordinate the lifecycle of multiple [**fibers**]({{< relref "fibers.md" >}}).

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
