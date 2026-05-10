# FsFlow Design: The Exit/Cause Model

This document outlines the transition from a `Result`-based effect model to a ZIO-inspired `Exit`/`Cause` model. This shift is foundational for providing structured concurrency, robust cancellation, and defect isolation across .NET and Fable.

## 1. The Problem with `Result<'v, 'e>`

Currently, `Flow<'env, 'e, 'v>` is defined as returning a `Result<'v, 'e>`. This has several limitations:

1.  **Mixed Concerns:** Unexpected exceptions (defects) and cancellation are "ambient" and bypass the `Result` channel.
2.  **Brittle Cancellation:** Cancellation is handled via `OperationCanceledException`, which can be accidentally caught or swallowed by user code.
3.  **Inconsistent Cleanup:** Resource cleanup depends on the caller correctly handling the exception stack.
4.  **Poor Observability:** There is no first-class way to distinguish between a "Domain Error" (typed failure) and a "System Signal" (interruption).

## 2. The New Core Types

We introduce `Cause<'e>` and `Exit<'v, 'e>` to represent the full range of workflow outcomes.

```fsharp
[<RequireQualifiedAccess>]
type Cause<'e> =
    | Fail of 'e            // Expected domain failure (typed)
    | Die of exn            // Unexpected defect/panic (untyped)
    | Interrupt             // Administrative signal to stop (cancellation)

[<RequireQualifiedAccess>]
type Exit<'v, 'e> =
    | Success of 'v
    | Failure of Cause<'e>
```

### The Unified Flow Signature
The core `Flow` type is updated to use `Exit` instead of `Result`:

```fsharp
#if FABLE_COMPILER
type Effect<'v, 'e> = JS.Promise<Exit<'v, 'e>>
#else
type Effect<'v, 'e> = ValueTask<Exit<'v, 'e>>
#endif

type Flow<'env, 'e, 'v> = 'env -> CancellationToken -> Effect<'v, 'e>
```

## 3. Semantics of the Exit Channels

| Channel | Trigger | Builder Behavior | Execution Boundary |
| :--- | :--- | :--- | :--- |
| **Success** | `return v` | Continues to next `bind` | Returns `Ok v` (or similar) |
| **Fail** | `fail e` | Short-circuits (typed) | Returns `Error e` |
| **Die** | `throw exn` | Short-circuits (untyped) | Rethrows or returns `Defect exn` |
| **Interrupt** | `ct.Cancel()` | Short-circuits (runtime) | Returns `Canceled` |

### Comparison to .NET Task
In .NET, a `Task` has `Status`: `RanToCompletion`, `Faulted`, or `Canceled`. 
The `Exit` model maps these directly into the data type, allowing the `flow { }` builder to handle them algebraically rather than relying on the CLR's exception unwinding.

## 4. Impact on `FlowBuilder` (The CE)

The `flow { }` builder is updated to handle the `Exit` state at every step.

- **`Bind` overloads:** When binding a `Flow`, the builder checks the `Exit` status. If it is `Failure`, the rest of the workflow is skipped, and the failure is propagated.
- **`TryFinally` / `Using`:** The runtime guarantees that `finally` blocks and `Dispose()` calls run even during `Interrupt` or `Die` states. This is implemented by the builder's state machine.

## 5. Migration Path (Phase 2.5)

1.  **Core Types:** Define `Cause` and `Exit` in `FsFlow/Core.fs`.
2.  **Signature Change:** Update `Flow` and `Effect` type aliases.
3.  **Builder Update:** Refactor `FlowBuilder` in `FsFlow/Builders.fs` to orchestrate `Exit` instead of `Result`.
4.  **Primitive Update:** Update `Flow.ok`, `Flow.fail`, `Flow.run`, etc., in `FsFlow/Flow.fs`.
5.  **Compatibility:** Provide helpers like `Flow.toResult` and `Flow.fromResult` for easy interop with existing code.

## 6. Benefits for Phase 3 (Runtime)

- **`Flow.race`:** When one flow wins, the other is **Interrupted**. Because `Interrupt` is part of the `Exit` type, the loser can clean up resources safely without the "race condition" of exception timing.
- **Fibers:** The Fiber runtime can track the `Cause` of failure across spawned children, providing "Causal Stack Traces."
- **STM:** Retries in STM can be modeled as a specialized `Cause` if needed.

## Conclusion

Moving to an `Exit`-based model transforms FsFlow from a functional utility library into a robust, supervised execution environment. It provides the "Structured Concurrency" guarantees that make ZIO and Erlang highly reliable.
