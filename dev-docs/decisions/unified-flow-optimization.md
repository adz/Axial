# Unified Flow and Hybrid Interop Optimization

Status: decided.
Recorded: 2026-05-17.

## Context

The original `Axial` design used three distinct workflow types (`Flow`, `AsyncFlow`, `TaskFlow`) to handle different execution models. This caused significant friction in composition and documentation. 

Furthermore, the initial `flow {}` implementation relied heavily on function closures for every `Bind` and `Return` step, leading to high heap allocation overhead compared to standard .NET `task {}` expressions.

## Goals

1.  **Unify the surface**: A single `Flow<'env, 'error, 'value>` type that handles sync, async, and task-based work.
2.  **Optimize interop**: Enable zero-boilerplate binding of `Task`, `ValueTask`, and `Async` directly inside `flow {}`.
3.  **Reduce allocations**: Bring `flow {}` performance closer to native `task {}` on .NET.

## Research: The Resumable State Machine Attempt

We investigated using F# 6 **Resumable State Machines** (`ResumableCode`) to achieve zero-allocation binds. This involved:
-   Storing the environment and cancellation token in a struct-based state machine.
-   Using the `__stateMachine` compiler intrinsic to stitch binds together into a single struct.

### The Problem: InvalidProgramException
During research, we encountered frequent `InvalidProgramException` crashes at runtime. The F# compiler (as of F# 6/7) has stability issues when:
1.  Custom resumable data structs carry **generic types** (like the environment `'Env`).
2.  The state machine needs to be hoisted across complex closure boundaries (like nested concurrency combinators).

Because these crashes occur in the JIT/CLR and are nearly impossible to debug, we deemed a pure "Option C" (Resumable Structs) too risky for a general-purpose library.

## The Decision: Hybrid Optimization

We landed on a **Hybrid Approach** that maximizes performance on .NET while maintaining the safety and cross-platform compatibility (Fable) of the core model.

### Key Implementation Details

1.  **ValueTask Backbone**: The internal `Execution` carrier on .NET is now `ValueTask<Exit<'v, 'e>>`. This enables a synchronous fast-path for flows that complete without yielding.
2.  **Builder Overloads**: `FlowBuilder` methods provide type-directed overloads for common interop carriers.
3.  **Direct Interop**: Explicit `Bind` and `ReturnFrom` overloads were added for `Task`, `ValueTask`, and `Async`. 
4.  **Internal State Machines**: Instead of wrapping tasks in a `Flow` closure before binding, we use the compiler's built-in `task {}` or `async {}` infrastructure *inside* the inlined bind step. This eliminates the "adapter tax" for interop.

## Consequences

### Performance
-   **Mixed Workflows**: Memory usage for workflows involving external `Task` calls is reduced by ~35% because manual adapters are gone.
-   **Pure Workflows**: Simple synchronous chains have a slightly higher memory floor (~1.0 KB) due to the safe `ValueTask` state machine overhead, but gain 100% stability.
-   **Speed**: Execution speed for interop-heavy code is significantly improved by reducing closure chain depth.

### Ergonomics
-   Users no longer need to call `Flow.fromTask` or `Flow.fromAsync`.
-   `let! x = myTask` works natively inside `flow {}`.

### Maintenance
-   The library now requires two builder implementations (using `#if FABLE_COMPILER`) to ensure optimal performance on .NET while keeping Fable compatibility.
-   The `Flow` constructor is internal-but-accessible to support the inlining strategy.

## Future Directions

As the F# compiler's support for custom resumable state machines matures, we may revisit a full struct-based backbone for `Flow`. For now, the **Hybrid Optimization** provides the best balance of speed, safety, and ergonomics.
