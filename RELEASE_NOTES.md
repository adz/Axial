# Release Notes

## 0.5.0 - 2026-05-17

- **Unified Flow Model**: Consolidated `AsyncFlow` and `TaskFlow` into a single, high-performance `Flow` type that works across all supported platforms (including Fable 5).
- **ZIO-Style Execution Semantics**: Introduced a robust `Exit` and `Cause` model that preserves the distinction between typed failures (`Fail`), cancellations (`Interrupt`), and unhandled defects (`Die`).
- **Structured Concurrency**: Added first-class support for fibers with `fork`, `join`, and `interrupt`, along with parallel orchestration primitives like `zipPar` and `race`.
- **Software Transactional Memory (STM)**: Implemented a composable STM engine with `TRef`, `retry`, `orElse`, and the `stm {}` computation expression for atomic state transitions.
- **Effectful Streams**: Introduced `FlowStream` with built-in backpressure and native `IAsyncEnumerable` interop for processing asynchronous data sequences.
- **Runtime Foundation**: Implemented a new internal `RuntimeRegistry` and `Scope` system for explicit service management and deterministic resource teardown.
- **Capability Redesign**: Migrated to nominal capability contracts using standard F# interfaces, making workflow signatures more readable and stable.
- **Fable 5 & Cross-Platform Support**: Full support for Fable 5 with a unified asynchronous strategy that remains performant on both .NET and JS targets.
- **Telemetry & Hosting**: Added `FsFlow.Hosting` for seamless DI integration and `FsFlow.Runtime.Telemetry` for automatic distributed tracing and activity tagging.
- **Documentation Reorganization**: Completely restructured the documentation site with a hierarchical sidebar, new tutorials on dependency management, and a comprehensive API reference.

## 0.4.0 - 2026-05-03

- Introduced **Tuple-Based Smart Binds** in `flow {}`, `asyncFlow {}`, and `taskFlow {}` for a concise "unwrap or fail" DX
- Added `orFailTo` semantic label to clarify domain error attachment in smart binds
- Expanded `TaskFlow` smart binds to support `Task<Option<_>>`, `Task<ValueOption<_>>`, `ValueTask<Option<_>>`, and `ValueTask<ValueOption<_>>`
- Major documentation overhaul with **function-level granularity** mirroring FsToolkit.ErrorHandling
- Enriched every public API member with detailed XML documentation (summary, remarks, parameters, returns)
- Added **expected output demonstrations** to validation and diagnostics guides
- New **"For AI Agents"** guide and machine-optimized `llms.txt` for better LLM assistance
- Improved site accessibility with better contrast and verified all documentation links

## 0.3.0 - 2026-05-02

- Major architectural shift to a workflow family: `Flow`, `AsyncFlow`, and `TaskFlow`
- Introduced `FsFlow.Net` package for .NET task-oriented workflows and interop
- Added `ColdTask<'value>` for deferred, restartable task factories
- Migrated documentation to a versioned Docusaurus site with generated runnable examples
- Reorganized the docs into a clearer product-manual path across getting started, execution semantics, runtime interop, environment slicing, and architecture
- Added package-oriented API landing pages for `FsFlow` and `FsFlow.Net`
- Trimmed the README into a shorter NuGet-facing entry point
- Added pure validation helpers and effect bridges for `Async` and `Task`
- Expanded benchmark suite with BenchmarkDotNet and new comparison scenarios

## 0.2.0 - 2026-04-28

- Second public preview release of `FsFlow`
- Completed the package and repository identity move to `FsFlow` across project files, examples, tests, docs, and packaging metadata
- Refreshed the docs site presentation and bundled docs assets for the renamed package
- Cleaned up solution and workflow references after the `v0.1.0` release
- Kept the public `Flow` API stable while polishing the package surface before larger follow-up changes

## 0.1.0 - 2026-04-26

- Initial public preview release of `FsFlow`
- Core `Flow<'env, 'error, 'value>` abstraction for explicit environment requirements, typed failures, and cold execution
- Direct `Result`, `Async`, `Task`, and `ColdTask` interop inside one `flow {}` workflow
- Runtime helpers for cancellation, timeout, retry, logging, and scoped cleanup
- User-facing guides for getting started, environment slicing, semantics, task and async interop, and supported architectural styles
- Runnable example applications plus a NativeAOT probe
- NuGet packaging metadata, symbols, SourceLink, and GitHub Pages API docs pipeline

## Release Process

Publish versions as Git tags such as `v0.2.0`.

The GitHub release workflow builds the package artifacts and attaches them to a GitHub Release.
NuGet publishing stays manual.
