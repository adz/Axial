# Proposal: Unified ZIO-style Architecture for FsFlow (Fable 5 Ready)

This document proposes a transition to a **Unified Concrete Effect** architecture. While inspired by Tagless Final "Algebra" (like Shopfoo), it adopts the **ZIO** model of a single, powerful data type to achieve "One Builder to Rule Them All" without the complexity of SRTP.

## Core Goals
- **Unified DX:** One `flow { }` builder for all effects (Sync, Async, Task, JS Promise).
- **Clean Stack Traces:** Leverage F# 6+ Resumable Code to flatten monadic steps into a single state machine.
- **Deep Observability:** Use a Fiber runtime to provide virtual stack traces and snapshotting across concurrent operations.
- **Fable 5 Native:** Direct mapping to JS `Promise` for maximum performance and browser interop.

## 1. The Core Type: The "Wide" Effect

Instead of maintaining `Flow`, `AsyncFlow`, and `TaskFlow`, we collapse them into a single, portable type.

### The Unified Type Alias
We use a platform-aware type alias to ensure maximum performance on .NET and native behavior on Fable 5.

```fsharp
#if FABLE_COMPILER
type Effect<'v, 'e> = JS.Promise<Result<'v, 'e>>
#else
type Effect<'v, 'e> = System.Threading.Tasks.ValueTask<Result<'v, 'e>>
#endif

type Flow<'env, 'err, 'res> = 'env -> CancellationToken -> Effect<'res, 'err>
```

**Why this works:**
- **.NET:** `ValueTask` provides zero-allocation for synchronous completions, making it faster than `Async` or `Task` for simple logic.
- **Fable 5:** Fable 5 transpiles `Task` and `ValueTask` directly to JS `Promise`. This means our `Flow` becomes a native, non-blocking JS computation.
- **Unified:** Every "Instruction" (like `Clock.now()`) now returns this same `Flow` type.

---

## 2. The Universal `flow { }` Builder

We achieve the "same builder" goal using **Method Overloading** and **Resumable Code**.

### Resumable State Machines (F# 6+)
Instead of traditional closure-based builders (which create a nested function for every `let!`), the `flow` builder will be implemented using **Resumable Code**. 

- **Flattened Stacks:** The entire workflow is compiled into a single state machine struct. Stack traces show a single `MoveNext` instead of a deep "staircase" of nested closures.
- **Zero-Allocation:** Synchronous "hot paths" (e.g., cached reads) execute without heap allocations.

### Overload Resolution
The `FlowBuilder` will provide `Bind` overloads for every common type:

```fsharp
type FlowBuilder() =
    // Primary Bind (Flow to Flow)
    member inline _.Bind(flw: Flow<'env, 'e, 'v>, [<InlineIfLambda>] binder) = ...
    
    // Auto-Lifting Binds
    member inline _.Bind(res: Result<'v, 'e>, [<InlineIfLambda>] binder) = ...
    member inline _.Bind(asn: Async<'v>, [<InlineIfLambda>] binder) = ...
    member inline _.Bind(tsk: Task<'v>, [<InlineIfLambda>] binder) = ...
    
    #if FABLE_COMPILER
    member inline _.Bind(prm: JS.Promise<'v>, [<InlineIfLambda>] binder) = ...
    #endif
    
    // Support for external effects (e.g., fio)
    member inline _.Bind(eff: IEffect<'v, 'e>, [<InlineIfLambda>] binder) = ...
```

---

## 3. ZIO Feature Set (Portable & Simplified)

By standardizing on a single `Flow` type, we can implement advanced ZIO features that work identically on .NET and Fable.

### A. Fibers (Concurrency & Observability)
Fibers are logical threads managed by the FsFlow runtime.
- **Virtual Stack Traces:** Because the runtime manages fiber lifecycles, it can stitch together "causal" stack traces. If a child fiber fails, the error includes the trace of the parent fiber that spawned it.
- **System Snapshots:** A built-in "Fiber Supervisor" allows developers to inspect all currently running fibers, helping identify "hanging" tasks or resource leaks.
- **Execution:** 
    - **.NET:** Maps to `Task.Run` or custom `Fiber` scheduling.
    - **Fable:** Maps to `setTimeout(0)` or `queueMicrotask` to prevent blocking the main thread.
- **Primitives:** `Flow.fork`, `Flow.join`, `Flow.interrupt`.

### B. Software Transactional Memory (STM)
A portable implementation of `Ref` and `TRef` that allows atomic state updates. On JS, this is naturally thread-safe (single-threaded loop), but the API remains consistent for .NET concurrency.

### C. Streams (`FlowStream`)
A unified `IAsyncEnumerable`-based stream (supported by Fable 5) that allows environment-aware, error-typed streaming.

### D. Native Cancellation
Since `CancellationToken` is now a core part of the `Flow` signature, we get **Native Interop** with .NET's cancellation system and Fable's abort-controller logic.

---

## 4. Pros & Cons

### Pros
- **Fable 5 First-Class:** No more "Is this supported in Fable?" anxiety.
- **Clean Observability:** Virtual stack traces and fiber snapshotting make debugging concurrent code a breeze.
- **Performance:** Resumable code ensures the unified flow is as fast as native C# `async/await`.
- **Zero SRTP Maze:** Users get standard, readable compiler errors. "Go to Definition" works perfectly.
- **Extensible:** Adding support for a new library (like `fio`) just requires adding a single `Bind` overload.

### Cons
- **Breaking Change:** This is a major version shift. Existing `asyncFlow` and `taskFlow` code must be migrated to `flow`.
- **ValueTask vs Task:** .NET users must be aware of `ValueTask` semantics (e.g., don't double-await), though the `flow` builder handles this safely for them.

---

## 5. Implementation Roadmap

1.  **Phase 1 (The Core):** Create the unified `Flow` type and the **Resumable** `flow` builder. 
2.  **Phase 2 (The Bridge):** Port `FsFlow.Caps.Core` to return the new unified `Flow`.
3.  **Phase 3 (ZIO Core):** Implement the **Fiber Runtime** with Virtual Tracing and Supervision.
4.  **Phase 4 (Advanced):** Port/Build `FlowStream` and `STM` on top of the new core.
5.  **Phase 5 (Compatibility):** Add interpreters for `fio` and other F# effect libraries.

---

## 6. Empirical Verification (Probe Results)

The viability of this architecture has been verified via an empirical probe (`dev-docs/fable-v5-probe.fsx`) executed on both the **.NET 10 CLR** and **Node.js (via Fable 5.0.0-alpha.12)**.

### Results:
- **One Builder, Three Effects:** The probe successfully orchestrated a native `Flow`, a `.NET Task`, and a `Result` within a single `flow { }` expression.
- **Overload Success:** The compiler correctly resolved overloaded `Bind` methods without requiring SRTP, providing a clean developer experience.
- **Cross-Platform Execution:** 
    - **.NET:** Produced `Ok "test-env-42-success"` using high-performance `ValueTask` paths.
    - **Node.js:** Produced the identical `Ok "test-env-42-success"` after transpilation, confirming that the `ValueTask`/`Promise` bridge is transparent to the user.
- **Fable 5 Interop:** Confirmed that Fable 5's native `Task` support and `promise { }` builder provide a robust foundation for this "Wide Effect" model.

---

## Conclusion
By leveraging Resumable Code and a Fiber-based runtime, FsFlow becomes more than just a ReaderT library—it becomes a **high-performance, observable functional runtime** that rivals ZIO in power while remaining uniquely F#-idiomatic.
