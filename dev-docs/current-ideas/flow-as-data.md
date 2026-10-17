# Flow as data: one description, many runtimes

Status: unaccepted backend proposal. The separate .NET tail-continuation trampoline is implemented; the typed-node
representation and multiple backends below are not. They are not prerequisites for fixing recursive-loop retention.

## Goal

Make `Flow<'env, 'error, 'value>` a description of work rather than a closure that performs it, so the same flow can be
run by different runtimes — .NET `ValueTask`, a continuation-based fiber runtime in the style of FIO, JS `Promise`,
BEAM — without changing the user-facing type or API.

Two constraints are not negotiable:

- **The user-facing surface does not change.** `Flow<'env, 'error, 'value>`, the `flow { }` builder, the `Flow` module
  and the execution members (`StartAsTask`, `StartAsValueTask`, `ToAsync`, `RunSynchronously`) keep their shapes. No carrier type parameter.
- **It must be faster than today, not merely acceptable.** A data representation that costs throughput is a failed
  experiment, however clean it is.

## What Flow is today

`Flow` still uses opaque closures for primitive operations. On .NET its internal sequencing cases expose only a
factory or a continuation (`src/Axial/Core.fs`):

```fsharp
type Flow<'env, 'error, 'value> =
    internal
    | Flow of ('env -> CancellationToken -> Execution<'value, 'error>)
    | FlowDelay of (unit -> Flow<'env, 'error, 'value>)
    | FlowContinue of ('env -> CancellationToken -> FlowStep<'env, 'error, 'value>)
```

`Execution` is `ValueTask<Exit<..>>` on .NET and `Async<Exit<..>>` under Fable (`src/Axial/Platform.fs`). The carrier
is fixed per platform at compile time. Fable keeps just the closure case and its existing `Async` sequencing.

On .NET, `Flow.bind` evaluates its source and hands the successor to an iterative executor. `Flow.delay` hands over
its factory. The executor consumes these tail continuations without retaining awaiting tasks for earlier iterations.
A `FlowStep` is a successor, a failure cause, or a typed frame holding a pending source Task and its continuation.
The executor awaits that Task directly, then consumes the frame. Failures terminate sequencing without building
another Flow. Synchronous completion still avoids an async state
machine. This is an execution fix, not a fully inspectable tree.

| Per `let!`, today | .NET | Fable |
|---|---|---|
| Flow wrapper and continuation closures | yes | yes |
| `Exit.Success` allocated for intermediate values | yes | yes |
| `env` and `CancellationToken` threaded through calls | yes | yes |
| `guardStack`: thread-static depth read/write plus `try/finally` at source boundaries | yes | no |
| `Task.Run` stack hops for deeply nested synchronous sources and opaque wrappers | yes | no |
| An F# `Async` bind, even when every step is synchronous | no | yes |

Tail sequencing on .NET runs in a loop. Nested non-tail sources and opaque combinators still use depth tracking and
periodic thread-pool hops. On Fable, every bind pays for `Async` regardless of whether anything is asynchronous.

The executor can inspect tail sequencing, but primitive operations remain opaque. Full operation inspection,
build-time fusion, and backend preparation are the separate opportunities this proposal explores.

## Prior attempt: carrier-indexed Flow (rejected)

An earlier spike (branch `spike-carrier-indexed-flow`, tip `f13f6da5`, since deleted) put the carrier in the type:
`Flow<'carrier, 'env, 'error, 'value>`, hidden behind per-carrier aliases in `FsFlow.ValueTask`, `FsFlow.Async` and
`FsFlow.Promise` packages. Its aim was carrier-polymorphic service packages. It was rejected for:

- **Arity leakage.** Library authors see the fourth parameter, and F#'s value restriction forces explicit type
  parameters on let-bound values that stay generic over the carrier.
- **Builder duplication.** `flow { }` is awkward to share across carrier packages.
- **Carrier-specific operations.** `Layer.zipPar` and other parallel operations must live in each carrier package.
- **Cross-carrier conversion.** It has real semantics to answer: cancellation onto `AbortController`, finalizers when
  a promise is abandoned, how rejection maps to `Cause`.
- **Name collisions.** Two `Flow` modules when both namespaces are open.

`main` instead hid the carrier inside a three-parameter `Flow` with per-platform execution members. This sketch keeps
that surface and moves the carrier choice out of the representation entirely.

## Design

### Not a generic tree

The textbook shape is a union of opcodes (`Succeed of obj | Bind of Flow * (obj -> Flow) | ...`) walked by an
interpreter. On .NET that erases types to `obj`, and boxing value types — `int`, `struct` tuples, small records —
allocates on every step. That alone could cancel any gain. Flow does not need to be a generic tree to be data.

### Typed node classes

`Flow<'env, 'error, 'value>` becomes an abstract class. Each primitive is a sealed subclass that holds its parts at
their real types:

```fsharp
[<AbstractClass>]
type Flow<'env, 'error, 'value> internal () =
    abstract internal RunSync      : Fiber<'env> -> SyncStep<'value, 'error>
    abstract internal RunValueTask : Fiber<'env> -> ValueTask<Exit<'value, 'error>>
    abstract internal RunCont      : Fiber<'env> * Cont<'value, 'error> -> unit
    // Fable: RunPromise, and whatever the BEAM runtime needs

[<Sealed>]
type internal Succeed<'env, 'error, 'value>(value: 'value) =
    inherit Flow<'env, 'error, 'value>()
    member _.Value = value
    // ...

[<Sealed>]
type internal Bind<'env, 'error, 'a, 'b>(first: Flow<'env, 'error, 'a>, next: 'a -> Flow<'env, 'error, 'b>) =
    inherit Flow<'env, 'error, 'b>()
    // ...
```

- **No boxing.** `Bind<'a, 'b>` calls its typed continuation directly.
- **Still data.** A flow can be walked, and combinators can inspect their inputs when a flow is built.
- **Cheap dispatch.** Each step is one virtual call on a sealed class, comparable to today's closure invoke.
- **Cost of a new runtime.** Adding a runtime means adding a method to every node class. The set of runtimes is small
  and changes rarely.

The current union case is `internal`, so replacing the union with an abstract class does not change what users can
write. The ApiShape tests pin the public surface and confirm this.

### A typed visitor, for runtimes that are not built in

Each node also implements `abstract Accept<'r> : IFlowVisitor<'env, 'error, 'value, 'r> -> 'r`. A new or experimental
runtime, a tracer, or an analysis pass is a visitor class; generic methods keep the types, so there is no boxing.
Generic virtual method calls are slower on .NET and fragile under NativeAOT, so visitors stay off the hot path. Shipped
runtimes use the per-runtime methods above.

### Rewrite when the flow is built

Smart constructors simplify as nodes are created, which a closure never allows:

- `succeed x >>= f` becomes `f x`;
- `map f (map g m)` becomes `map (f << g) m`;
- `map f (succeed x)` becomes `succeed (f x)`;
- left-nested binds are reassociated, so a deep `let!` chain does not nest.

Rewrites must preserve semantics exactly. Anything that can throw, observe cancellation, or register a finalizer is
not moved across those boundaries.

### Runner internals

- **Explicit continuation stack.** The runner keeps one reusable array of pending continuations per fiber instead of
  recursing. That makes flows stack safe by construction and removes `guardStack`, its thread-static access, its
  `try/finally`, and the `Task.Run` hops.
- **Synchronous fast path everywhere.** `RunSync` loops over synchronous steps holding the current value directly. It
  only builds a `ValueTask`, `Promise` or `Async` at a real asynchronous boundary. This is the main win on Fable.
- **`Exit` only on failure and at the end.** Success values stay unwrapped; failure takes a separate path.
- **Fiber holds the context.** `env`, the cancellation token and runtime state live on the fiber, not in every call.

### Prepare once, run many (later)

Flows are often module-level values run many times, such as once per request. A runtime could turn a flow into a
prepared executor on first run and cache it, so the data stays the single definition and the prepared form is the
fast path. This is an optimisation to reach for only after the basic design is proven faster.

## Where the speed should come from

| Platform | Expected source of gain |
|---|---|
| .NET | Removing `guardStack` and the stack hops; one allocation per node instead of two closures; no `Exit` per step; build-time rewrites |
| Fable (Node, BEAM) | No `Async` bind for synchronous stretches; build-time rewrites |

The .NET gain is the less certain of the two, because the synchronous fast path already exists there. The phase 1
spike exists to measure it.

## Risks

- **Cancellation, scopes and finalizers.** Finalizers must run in the right order when a flow is cancelled mid-way or
  fails, and cancellation must be observed at the same points as today. Each runtime reimplements these rules; most
  subtle bugs will be here.
- **Concurrency.** Parallel steps, races, forked fibers and supervision must behave identically under every runtime.
- **Streams.** `Stream.fs` is built directly on `Execution` (61 call sites) and needs its own port.
- **Fable.** Type tests on generic classes behave differently in JS (generics are erased), so build-time rewrites must
  be checked on Fable. BEAM is a third target with its own performance profile.
- **NativeAOT.** Keep generic virtual methods out of the hot path, and verify with the `aot-audit` skill.
- **Satellite packages.** 17 assemblies have `InternalsVisibleTo` access (`src/Axial/AssemblyInfo.fs`), and some build
  flows directly (Axial.Telemetry in 8 places, Axial.Telemetry.JavaScript in 3).

## Size of the change

Measured on `main` at `20ee18d0`:

- `src/Axial/Flow.fs`: about 80 sites that build or unwrap the closure directly.
- Rest of `src/Axial`: about 30 more, in Runtime, Concurrency, Schedule, FlowBuilder, Stream, Interop, Core and State.
- `Execution.*` helpers: about 170 calls — Flow.fs 63, Stream.fs 61, Axial.Layers 15, Schedule.fs 14, the rest small.
- Tests: about 308. Because the public API does not change, they are the oracle for behaviour.

Most of the ~80 sites in Flow.fs are combinators expressible in terms of about ten primitives. Only the primitives
become node classes.

## Migration

Do it gradually, never as a long-lived broken branch. The first node is `FromExecution`, which wraps today's
`'env -> CancellationToken -> Execution<..>` closure, so every existing combinator keeps working unchanged. Combinators
then move from `FromExecution` to real nodes one at a time, with the suite green at every commit. When nothing
constructs `FromExecution` any more, it can be removed or kept as the interop escape hatch.

## Phases

| Phase | Work | Effort |
|---|---|---|
| 0 | Fill gaps in the benchmarks (below) | ~1 day |
| 1 | Spike: abstract `Flow` with the core primitives, `ValueTask` runner with the synchronous fast path and continuation stack, build-time rewrites. **Go/no-go.** | 3–5 days |
| 2 | Port Flow.fs combinators through `FromExecution` | 1–2 weeks |
| 3 | Cancellation, scopes, finalizers, concurrency, Runtime, Schedule | 1–2 weeks |
| 4 | Stream, Layers, Telemetry and the other satellite packages | ~1 week |
| 5 | Fable runners (Promise, BEAM), AOT check, performance tuning | ~1 week |
| 6 (optional) | A continuation-based runner in the FIO style, to prove the multiple-runtime goal | ~1 week |

Roughly 5–8 weeks of focused work. Phase 3 is the bottleneck: it is careful semantic review, and it is where the
design decisions surface.

Core primitives for phase 1: succeed, fail, bind, map, read environment, asynchronous boundary, finalizer.

**Go/no-go for phase 1:** the spike must beat current `main` on the synchronous-heavy cases on .NET and on Fable
(Node), and must not lose on the asynchronous-heavy cases. If it does not, stop and rethink before phase 2.

## Benchmarks

`benchmarks/Axial.Benchmarks` already covers composition chains (`Flow map x100`, `Flow bind x100`, bind async and
task variants), synchronous completion against a candidate `ValueTask` flow and Ply, reader overhead, cancellation, and
railway comparisons against FsToolkit. `scripts/run-fable-benchmarks.sh` runs the Fable suite on Node and Erlang.

Add before phase 1:

- a deep synchronous chain well past `MaxSynchronousDepth` (e.g. 10,000 binds), to expose the stack-hop cost;
- the same flow value run many times, to measure build-once versus run-many;
- struct-valued pipelines, to catch boxing;
- mixed synchronous and asynchronous steps in realistic proportions;
- allocation counts (`MemoryDiagnoser`) on all of the above, not just time.

## Open questions

- Should the continuation stack live on the fiber, or be pooled per thread?
- Which operations are primitives, and which are combinators over them? The smaller the primitive set, the less each
  runtime has to implement, but the fewer rewrites are possible.
- Does `RunSync` return a discriminated step (`Done | Failed | Suspended`), or does the runner switch modes?
- Is prepare-once worth its cache and invalidation complexity once the basic runner is fast?
- Does the BEAM target want a runtime of its own rather than a Promise-style one?
