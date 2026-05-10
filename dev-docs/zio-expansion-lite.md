# FsFlow: ZIO Expansion Lite Roadmap

This document outlines the strategy for expanding FsFlow with high-value functional runtime features inspired by ZIO and Effect-TS, while maintaining a "BCL-first" philosophy. The goal is to provide the power of structured concurrency and resilient orchestration without replacing the .NET standard library.

## Philosophy: Functional Glue for .NET

FsFlow does not aim to be a platform that replaces .NET; it aims to be the **functional glue** that makes .NET types (Tasks, Channels, Streams) safer, more composable, and easier to test.

- **Unified Flow:** One type (`Flow<'env, 'e, 'v>`) for all effects.
- **Exit Model:** Algebraic handling of success, failure, defects, and interruption.
- **BCL-First:** Wrap and enhance existing .NET primitives instead of re-inventing them.

---

## 1. Supervised Agents (Highest Value)

Agents provide serialized access to state. Unlike `MailboxProcessor`, FsFlow agents are "supervised" by the `Exit` model and integrated into the `Flow` environment.

### Core Mechanics
- **Serialized Processing:** Message handling is strictly sequential (no locks needed for state).
- **Synchronous Heart:** The core state transformation is `state -> msg -> state`.
- **Flow Envelope:** Use `flow { }` for effects (logging, DB) and supervision during processing.
- **Supervision:** Use the `Schedule` module to define restart/retry policies when the processing loop fails or dies.

### Implementation Goals
- `FlowAgent.spawn`: Starts an agent as a supervised **Fiber**.
- **Structured Interruption:** When the parent fiber is interrupted, the agent's mailbox is closed, and the loop finishes its current message before shutting down.
- **Channel-Backed:** Use `System.Threading.Channels` as the underlying mailbox for high performance.

---

## 2. Contextual Metadata (`FiberRef`)

Fibers need to carry "out-of-band" context (Telemetry, Request IDs, Correlation IDs) that propagates automatically through forks and async boundaries.

### Implementation Goals
- **Automatic Propagation:** When a fiber is `forked`, the `FiberRef` values are inherited by the child.
- **Telemetry Bridge:** Native integration with `System.Diagnostics.Activity` to map `FiberRef` values to tracing tags.
- **Environment Parity:** Works across both .NET (AsyncLocal-backed) and Fable.

---

## 3. Synchronization Primitives

Provide functional, flow-aware versions of standard TPL primitives.

### Implementation Goals
- **`Deferred<'v>` (Functional Promise):** A single-assignment variable that allows fibers to synchronize. (Avoids the name "Promise" to prevent confusion with Fable/JS `Promise`).
- **`Semaphore`:** Limit concurrency within a flow-aware context.
- **`Latch`:** A simple "gate" that can be opened once, allowing multiple fibers to wait.

---

## 4. Communication (`Queue` and `Hub`)

Build functional abstractions on top of `System.Threading.Channels`.

### Implementation Goals
- **`FlowQueue`:** An environment-aware, error-typed wrapper for bounded/unbounded channels.
- **`FlowHub`:** A functional pub-sub mechanism (broadcast) that ensures backpressure across multiple subscribers.
- **Backpressure-Aware:** Operations like `offer` and `take` are `Flow` values that observe the runtime `CancellationToken`.

---

## 5. Fleshed-out Streams (`FlowStream`)

Move beyond a thin wrapper for `IAsyncEnumerable` to a full functional DSL.

### Implementation Goals
- **Flow-Aware Operators:** `mapFlow`, `filterFlow`, `collectFlow`, and `tapFlow` to access the environment for every element.
- **Error Handling:** Standardized `catch` and `retry` for stream sources.
- **Resource Management:** Ensure `IAsyncDisposable` sources are cleaned up even if the stream is interrupted mid-way.

---

## 6. Resource Scoping

Move from surgical `useWithAcquireRelease` to pervasive lifecycle management.

### Implementation Goals
- **Automatic `IAsyncDisposable`:** Robust support in `flow { }` for `use` and `use!` keywords.
- **Scoped Hand-off:** Support for a "Scope" environment that allows resources to be opened in a child flow and handed off to a parent for later cleanup.
- **Interruption Guarantee:** Ensure `release` logic runs even during `Cause.Die` or `Cause.Interrupt`.

---

## 7. Advanced STM (Software Transactional Memory)

Enhance the existing STM with composable blocking.

### Implementation Goals
- **`retry`:** Suspend a transaction until one of its accessed `TRef`s changes.
- **`orElse`:** Attempt one transaction, and if it calls `retry`, attempt another.
- **Wait-Free Reads:** Optimize for .NET's memory model where possible.

---

## 8. BCL Convergence (The .NET Advantage)

FsFlow should act as a high-level, "safe" overlay for common .NET primitives, transforming exception-based or untyped APIs into `Flow`-aware values.

### Implementation Goals
- **Resilient HttpClient:** 
    - Bridge `IHttpClientFactory` to `Flow`.
    - Provide automatic `Schedule`-based retries and `FiberRef` propagation (tracing headers).
    - Map status codes to typed `HttpError` cases (e.g., `404 -> NotFound`).
- **Typed Configuration:** 
    - Wrap `IConfiguration`. 
    - Use `validate {}` logic to ensure required keys exist and match types, returning a `Flow` failure instead of `null` or `KeyNotFoundException`.
- **Flow-Aware Rate Limiting:** 
    - Bridge `System.Threading.RateLimiting`.
    - Provide `Flow.acquirePermit` which respects the runtime `CancellationToken` and `Exit` model.
- **Algebraic Telemetry:** 
    - Integration with `System.Diagnostics.Activity`.
    - Automatically start/stop spans and attach `Exit` status (Success/Fail/Die/Interrupt) as tags.
- **Smart Caching:** 
    - Bridge `IMemoryCache` / `IDistributedCache`.
    - Implement "Cache-Aside" patterns that use `Deferred` or `STM` to prevent "cache stampedes" (multiple fibers fetching the same missing key simultaneously).

---

## 9. Strong F# Ecosystem Interop

FsFlow should be the "connective tissue" that solves problems existing F# libraries cannot solve alone due to their lack of a runtime environment.

### Implementation Goals
- **`flowValidate { }` (Effectful Parallel Validation):**
    - Bridge `Validus` or `FsToolkit.ErrorHandling.Validation` with `Flow`.
    - Allow `and!` to run pure checks and effectful `Flow` checks (e.g., DB uniqueness) concurrently via `zipPar`.
    - Accumulate failures from both sources into a single `Diagnostics` graph.
- **Unified Transactions (DB + STM):**
    - Bridge persistent libraries (Dapper, EF Core) with FsFlow STM.
    - Atomically commit or rollback both Database changes and `TRef` state updates.
    - Ensure automatic rollback on `Fiber` interruption or defect (`Cause.Die`).
- **CAPS Auto-Provisioning:**
    - While FsFlow has the `Needs<'T>` structural mechanic, it requires manual wiring to `IServiceProvider`.
    - Implement a source generator or reflection-based bridge that automatically satisfies `Needs<'T>` requirements by slicing them from a standard .NET container.
- **Fable React `useFlow` Hook:**
    - A dedicated hook for running flows from UI components with **Automatic Cancellation**. 
    - If the component unmounts, the fiber is automatically **Interrupted**, preventing state leaks or "set state on unmounted component" errors.
- **Worker-Backed Agents (Fable):**
    - Proxy `FlowAgent` execution to Web Workers with typed-error communication.
    - Offload heavy orchestration from the main UI thread while maintaining the `Flow` environment.


---

## 10. FlowApp & CLI (The Entry Point)

FsFlow should provide a standardized way to bootstrap applications that ensures structured shutdown and clean exit-code mapping.

### Implementation Goals
- **`FlowApp` Base Class:**
    - A standard entry point similar to `ZIOApp`.
    - **Signal Handling:** Automatically trap `SIGINT` (Ctrl+C) and `SIGTERM`, mapping them to a **Fiber Interruption** of the main application flow. This ensures all `use` blocks and `finally` clauses run before the process exits.
    - **Bootstrap Phase:** A specialized flow that runs before the main app to build the environment (`'env`) from configuration and CLI arguments.
- **Functional CLI Parsing:**
    - A "Flow-aware" wrapper for `Argu` or a native functional DSL.
    - Allows CLI arguments to be validated using `flowValidate { }` (e.g., checking if a `--config` file path actually exists).
- **Automatic Exit Codes:**
    - Map `Exit.Success` to `0`.
    - Map `Exit.Failure (Cause.Fail e)` to a non-zero code with a formatted error on `stderr`.
    - Map `Exit.Failure (Cause.Die ex)` to a non-zero code with a stack trace.
    - Map `Exit.Failure Cause.Interrupt` to a standard "Interrupted" code.

---

## 11. Advanced Reliability (The "Pro" Layer)

To match ZIO's production-grade resilience, FsFlow will implement several advanced primitives for resource management and observability.

### Implementation Goals
- **`FlowPool` (Resource Pooling):**
    - A backpressured, flow-aware pool for expensive resources.
    - Supports **Interruptible Acquisition**: if a fiber is interrupted while waiting for a resource, it leaves the queue immediately.
- **Composable Schedules:**
    - Refactor `Schedule` to support algebraic composition.
    - `Schedule.intersect (s1, s2)`: Runs both and stops when the first one stops.
    - `Schedule.union (s1, s2)`: Runs both and stops when both have stopped.
- **`SynchronizedRef`:**
    - An extension of `Ref` that allows `update` functions to return a `Flow`.
    - Guarantees that only one effectful update runs at a time (serialized) without blocking the underlying thread.
- **Fiber Introspection:**
    - A diagnostic registry for active fibers.
    - Provides a "Fiber Dump" utility to see which fibers are running, waiting, or stuck, along with their `Cause` of failure.

---

## 12. Cross-Platform Abstractions (Fable 5)

FsFlow leverages Fable 5 to provide a single, unified developer experience across .NET, JavaScript, and the BEAM (Erlang).

### Implementation Goals
- **Universal Monad:**
    - The `Flow<'env, 'e, 'v>` type is consistent across all platforms, returning a unified `Effect<'v, 'e>`.
    - User code remains 100% portable, regardless of whether it compiles to CIL, JS, or Erlang bytecode.
- **Platform-Specific Bridges:**
    - **JS/Fable:** Map `Fiber` and `Interruption` to native `AbortSignal` and `Promise` lifecycle.
    - **BEAM/Erlang:** Map `Fiber` and `FlowAgent` to native Erlang **Processes** (`Pid`), `links`, and `monitors`.
    - **.NET:** Map to `Task` and `CancellationToken`.
- **Target-Specific Directives:**
    - Use Fable 5 compiler symbols (`FABLE_COMPILER_JAVASCRIPT`, `FABLE_COMPILER_BEAM`) to optimize the runtime for the native strengths of each platform (e.g., private heaps on Erlang, event-loop efficiency on JS).
- **OTP Integration (Erlang):**
    - Provide a `Flow.toProcess` bridge to allow FsFlow agents to be supervised by native Erlang/OTP Supervision Trees.

---

## 13. Prioritized Roadmap

1.  **Phase A (Agents & Supervision):** `FlowAgent`, `Schedule`-based restarts, and basic supervision.
2.  **Phase B (Metadata & Sync):** `FiberRef`, `Deferred`, and `Telemetry` integration.
3.  **Phase C (Communication & Streams):** `Queue`, `Hub`, and advanced `FlowStream` operators.
4.  **Phase D (BCL & F# Interop):** `HttpClient`, `Config`, `useFlow` (Fable), and `Web Middleware`.
5.  **Phase E (Application Core):** `FlowApp`, Signal Handling, and CLI parsing.
6.  **Phase F (Advanced Reliability):** `FlowPool`, Composable Schedules, and `SynchronizedRef`.
7.  **Phase G (Advanced Concurrency):** `Semaphore`, `Latch`, and Advanced STM.
8.  **Phase H (Platform Specialization):** OTP Integration and Fable-specific runtime optimizations.
