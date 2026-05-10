# FsFlow Plan

This file tracks live product-shape direction and any remaining open questions.
`dev-docs/TASKS.md` is the executable backlog.
`dev-docs/decisions/README.md` indexes the settled decisions that no longer belong here.

## North Star: The Unified Effect Model

FsFlow is the cross-platform functional runtime for .NET and Fable.

Instead of choosing between `Flow`, `AsyncFlow`, and `TaskFlow`, the library provides a single, unified **`Flow`** type that orchestrates all effects—Sync, Async, Task, and Promise—using a single `flow { }` builder. 

The core progression is:

```text
Check -> Result -> Validation -> Flow (Exit Model)
```

The unified model leverages **Fable 5** to provide a consistent developer experience across the entire stack, from backend microservices to frontend web applications, with built-in support for ZIO-like features such as Fibers, STM, and Streams.

## Settled Decisions

... (previous items unchanged) ...

- [Unified ZIO-style Architecture](tagless-final-idea.md): Move to a single `Flow<'env, 'e, 'v>` type based on `ValueTask` (.NET) and `Promise` (Fable) to eliminate effect-family friction.
- [Exit/Cause Model](EXIT_CAUSE_PLAN.md): Use an explicit `Exit` type instead of `Result` to separate domain failures, defects, and interruption.

## Live Direction

The current priority is **Convergent Evolution**: merging the separate effect families into the Unified ZIO-style Architecture. The CAPS story remains foundational but will be delivered on top of this unified model.

### 1. The Unified Core
- Adopt a single `Flow<'env, 'err, 'res>` type defined as `'env -> CancellationToken -> Effect<'res, 'err>`.
- Use `ValueTask` on .NET and `Promise` on Fable 5.
- Transition from `Result<'v, 'e>` to `Exit<'v, 'e>` to support structured concurrency and robust interruption.
- Implement the universal `flow { }` builder using method overloading for `Async`, `Task`, `Result`, and environment requests.

### 2. ZIO Features (The Cross-Platform Runtime)
- **Fibers:** Light-weight concurrency that works on both the .NET ThreadPool and the JS Event Loop, powered by the `Exit.Interrupt` signal.
- **STM:** Software Transactional Memory for atomic state updates.
- **Streams:** Unified `FlowStream` for environment-aware, error-typed streaming with backpressure.
- **Scheduling:** Fluent retry and repeat logic.

### 3. CAPS on Unified Flow (Post-Unification)
- Once the core is unified, all CAPS packages will be updated to return the unified `Flow` type.
- This eliminates the need for `Async`-suffixed capability methods and allows CAPS to be used interchangeably across all boundaries.

Target package families (Post-Unification):
- `FsFlow.Caps.Core`
- `FsFlow.Caps.Context`
- `FsFlow.Caps.Observability`
- ... (rest of the families) ...

`dev-docs/TASKS.md` is the executable backlog for this phase.

## Done Means

- the docs read like product documentation for the user
- the API reference is useful without opening the source
- every public API is reachable from the side menu
- semantic edge cases are documented and tested
- the project feels like a maintained library, not a design notebook
