# FsFlow Tasks

This file is the active queue for `scripts/ralph-loop-tasks.sh`.
Keep completed work out of this file.
Keep settled design decisions in `dev-docs/decisions/`.
Keep live product and architecture direction in `dev-docs/PLAN.md`.

## Phase 1: The Unified Core (Convergence)

1. [x] Define the unified `Flow<'env, 'e, 'v>` type in `FsFlow/Core.fs` using a `ValueTask`/`Promise` bridge with `#if FABLE_COMPILER` guards; include the standard `Result` wrapping.
2. [x] Implement the core `Flow` module primitives (`ok`, `error`, `read`, `map`, `bind`, `tap`, `fromResult`) using the new unified signature.
3. [x] Implement the universal `flow { }` builder in `FsFlow/Builders.fs` using method overloading to support `Async`, `Task`, `Result`, and environment requests; ensure full `CancellationToken` propagation.
4. [x] Implement `Flow.run` and `Flow.runFull` (and `runWithToken`) for the unified type, supporting both synchronous and asynchronous execution paths on .NET and native Promise execution on Fable.
5. [x] Add unit tests for the unified `flow { }` builder covering mixed effect orchestration (Sync, Async, Task, Result) and verify against the Fable 5 transpilation mapping.

## Phase 2: Migration & Cleanup

6. [x] Migrate existing `FsFlow` internal modules (Guard, Validate) to use the new unified `Flow` type instead of separate `Flow`/`AsyncFlow`/`TaskFlow`.
7. [x] Refactor the project structure to remove the separate `AsyncFlow.fs` and `TaskFlow.fs` files, merging their unique logic (e.g., retries, timeouts) into the unified `Flow` module.
8. [x] Update existing unit tests and examples in `tests/FsFlow.Tests` and `examples/FsFlow.Examples` to use the unified `flow { }` builder.
9. [x] Regenerate API documentation to reflect the single-type model and the removal of the effect-family split.

## Phase 2.5: The Exit Model (Algebraic Interruption)

See [EXIT_CAUSE_PLAN.md](EXIT_CAUSE_PLAN.md) for the design.

10. [ ] Define `Cause<'e>` (Fail, Die, Interrupt) and `Exit<'v, 'e>` types in `FsFlow/Core.fs`.
11. [ ] Refactor the unified `Flow` signature to return `ValueTask<Exit<'v, 'e>>` (or `JS.Promise` on Fable).
12. [ ] Update the `flow { }` builder to handle the `Exit` channels; ensure `try...finally` and `use` blocks respect interruption signals.
13. [ ] Update core `Flow` primitives (`map`, `bind`, `catch`, `run`) to operate on the `Exit` model.
14. [ ] Implement `Flow.toResult` and `Flow.fromResult` interop helpers to maintain compatibility with standard F# results.

## Phase 3: ZIO Core Features (The Runtime)

15. [ ] Implement the `Fiber` abstraction for light-weight concurrency; provide `Flow.fork`, `Flow.join`, and `Flow.interrupt` using the `Exit.Interrupt` signal.
16. [ ] Implement `Flow.zipPar` and `Flow.race` using the Fiber runtime to enable high-performance parallel orchestration with structured interruption.
17. [ ] Implement Software Transactional Memory (STM) core: provide `Ref<'T>`, `TRef<'T>`, and the `stm { }` builder for atomic state updates.
18. [ ] Implement the `FlowStream<'env, 'e, 'v>` type: provide environment-aware, error-typed streaming with backpressure support (using `IAsyncEnumerable` for Fable 5 parity).
19. [ ] Implement the Scheduling API: provide fluent retry and repeat logic (e.g., `Schedule.exponential`, `Schedule.jittered`, `Schedule.recur`).


## Phase 4: Post-Unification CAPS

15. [ ] Refactor `FsFlow.Caps.Core` and `FsFlow.Caps.Context` to use the unified `Flow` type; remove `Async` suffixes from capability methods and ensure they are Fable-compatible.
16. [ ] Implement `FsFlow.Caps.Observability` on the unified model: provide integrated tracing and metrics that auto-capture context from the `Flow` environment.
17. [ ] Implement the remaining CAPS packages (Console, FileSystem, Http, Process) as unified-only effects.
18. [ ] Perform a full project-wide validation: run all tests, verify Fable 5 transpilation for all packages, and update all documentation for the FsFlow 2.0 (Unified) release.
