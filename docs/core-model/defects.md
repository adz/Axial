---
weight: 25
title: Defects and Exceptions
description: Why FsFlow separates domain failures, interruptions, and defects.
---

# Defects and Exceptions

FsFlow distinguishes between expected failures, administrative signals (interruption), and unexpected defects. This separation ensures that your domain logic remains clean while the runtime provides robust, leak-proof resource management.

## Quick Start: Usage Patterns

### Producing Failures
Choose the function that matches your intent:

| Intent | Function | Outcome |
| :--- | :--- | :--- |
| **Domain Error** (Expected) | `Flow.fail "Not found"` | `Cause.Fail "Not found"` |
| **Defect/Panic** (Bug) | `Flow.die (exn "Database down")` | `Cause.Die exn` |
| **Interruption** | `Flow.interrupt` or runtime cancellation | `Cause.Interrupt` |
| **Sequential Failures** | Workflow fails, then cleanup fails | `Cause.Then (workflowCause, cleanupCause)` |
| **Parallel Failures** | Parallel branches both fail | `Cause.Both (leftCause, rightCause)` |

### Bridging Exceptions
Use `Flow.catch` to convert specific exceptions into domain errors. Exceptions not caught by the handler will remain as `Cause.Die`.

```fsharp
let safeParse id =
    flow {
        let! json = Http.get id
        return Json.parse json
    }
    |> Flow.catch (function
        | :? JsonException as ex -> DomainError.InvalidFormat ex.Message
        | ex -> reraise ex) // Bubbles up as Cause.Die
```

---

## The "Why": Architectural Rationale

While standard F# practice favors "just using exceptions" for defects, FsFlow treats them as first-class data in the `Exit` type for three critical reasons.

### 1. Structural Integrity (The "Closed" Algebra)
In complex orchestration like `Flow.zipPar` (running two flows concurrently), the engine must coordinate the lifecycle of multiple [**fibers**]({{< relref "fibers.md" >}}).

*   **The Problem:** If a defect is just a thrown exception, it escapes the return value of the function. The engine would have to handle two disjoint failure paths: returning a failure value OR catching a thrown exception. This forces every combinator to use defensive `try...finally` blocks just to coordinate basic signaling.
*   **The Solution:** By capturing defects into the `Exit` type, every flow execution returns a value. This makes the algebra "closed." If one branch dies, the engine receives it as data, immediately triggers cancellation for the other branches, and returns a single, structured outcome.

### 2. Lossless Concurrency Coordination
When a fiber fails, you often need to perform cleanup (e.g., `ensuring` or `onExit`). 

By reifying defects into `Cause.Die`, FsFlow passes the exact cause, including the original exception and stack trace, to your finalizers as a value. This enables high-fidelity observability: you can log exactly why a background fiber died without crashing the host process, and without needing a `try...with` block inside every finalizer.

If cleanup itself fails after the workflow has already failed, FsFlow does not discard either side. It returns `Cause.Then (workflowCause, cleanupCause)` so observability and host boundaries can see the original failure and the cleanup defect in order.

### 3. Precision in Retries and Fallbacks
The distinction between `Fail` and `Die` allows for smarter defaults:
*   **Retries** should usually target `Fail` (e.g., a transient network error), but never `Die` (e.g., a `NullReferenceException`). Retrying a bug is usually a waste of resources.
*   **Fallbacks** (`orElse`) usually target domain failures. If a workflow has a defect, it usually indicates a corrupted state that fallback logic wasn't designed to handle.
