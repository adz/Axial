# FlowStream Proving Before 1.0

Status: active design sketch. This is not accepted architecture until the implementation work and proving slices below
validate it. Once accepted, fold durable rules into `dev-docs/PLAN.md` and delete this file.

Sequence this work immediately after the repository/package split described in `project-split.md`.

## Decision to make before 1.0

`FlowStream<'env, 'error, 'value>` should become the portable, resource-safe streaming model for Flow before its public
semantics are frozen at 1.0. Network, Serial, WebSocket, streaming HTTP/SSE, Compression, and the existing Process
package should prove those semantics through narrow vertical slices. Complete versions of those satellite packages are
not a prerequisite for 1.0; evidence that FlowStream can support them correctly is.

FlowStream must reuse Flow's execution model. It must not grow a second implementation of scopes, cancellation,
timeouts, retry, or defects.

## Goals

- Make stream acquisition, use, early termination, and release deterministic.
- Reuse `Flow.scoped`, `Flow.acquireRelease`, runtime cancellation, timeout, retry, and child scopes.
- Preserve cold execution, typed failure, explicit environment dependencies, and pull-based backpressure.
- Keep the public model portable across .NET and Fable, with room for future Python, Rust, and Erlang runtimes.
- Support long-lived, resource-owning streams without leaking handles or retaining unbounded data.
- Give common operations one obvious, qualified name that humans and LLMs can predict.
- Keep platform-native representations behind adapters rather than reducing every platform to one native mechanism.
- Prove behavior through Process plus narrow TCP, Serial, WebSocket, and SSE slices before 1.0.

## Non-goals

- Reproducing Reactive Extensions, Akka Streams, ZIO Streams, or every collection combinator.
- Owning a general scheduler, channel runtime, hub, or unbounded queue.
- Exposing `System.IO.Pipelines`, `IAsyncEnumerable`, Web Streams, Node streams, or other platform types in core
  FlowStream signatures.
- Making arbitrary concurrent use of one stream safe. FlowStream is a single-consumer pull abstraction.
- Providing zero-copy leased buffers as the default element type.
- Completing Network, Serial, WebSocket, SSE, or Compression before 1.0.

## Core semantic contract

### Execution

- A FlowStream is cold. Construction performs no operational effect.
- Each terminal consumption starts a fresh execution.
- One execution has one logical consumer. Concurrent pulls are rejected as misuse or prevented internally.
- Each successful pull emits at most one value. The next pull does not begin until the consumer asks for it.
- `Done` is stable: once observed, the source is never pulled again.
- Typed source failures remain in `'error`; defects and interruption retain normal Flow cause semantics.

### Resources

- Every resource-owning stream executes inside a child Flow scope.
- Acquisition uses Flow acquisition primitives, ultimately `Flow.acquireRelease`.
- Closing a stream closes its child scope exactly once.
- Cleanup order and cleanup defects use existing Flow scope behavior.
- Stream operators do not maintain an independent finalizer stack.
- A resource is released after normal completion, typed failure, interruption, consumer failure, or early termination.
- Ownership is explicit. A stream that acquires a resource closes it; a view over a caller-owned resource only releases
  state it acquired itself.

### Cancellation and time

- Runtime cancellation is Flow interruption, not an ordinary stream element or a newly invented stream error.
- Native adapters receive the runtime cancellation mechanism and use it to interrupt pending acquisition and pulls.
- A configured deadline is a specification value interpreted with Flow timeout combinators.
- Stream implementations must not recreate timeout racing already provided by Flow.
- Adapter-specific cancellation plumbing is allowed only to translate Flow cancellation into the native API, such as a
  .NET cancellation token, JavaScript `AbortSignal`, or closing a handle whose read API cannot otherwise be canceled.
- Idle or inter-element timeout is a stream policy built from Flow timing, not a native transport timeout hidden inside
  FlowStream.

### Backpressure and memory

- Pulling is the default backpressure mechanism.
- Prefetching and buffering are explicit, bounded policies.
- No operator introduces an unbounded queue.
- Operators document their maximum retained element count.
- Resource adapters do not read indefinitely after downstream stops pulling.
- Variable-sized framing and decoding always has an explicit maximum retained size.

### Portability

Core FlowStream concepts are limited to Flow concepts: environment, typed error, values, scopes, execution,
interruption, and pull. Core public APIs do not mention:

- .NET `Stream`, `Task`, `ValueTask`, `IAsyncEnumerable`, `PipeReader`, or `Channel`;
- browser `ReadableStream`, `WritableStream`, or `AbortSignal`;
- Node stream types;
- runtime-specific buffer ownership types.

Platform integrations adapt native sources into the same semantic contract. Conditional implementation code may live
behind the existing Flow platform boundary. Separate integration packages are justified when they expose native types;
the abstract FlowStream API itself does not split into `FlowStream.JavaScript`, `FlowStream.DotNet`, or one package per
runtime.

## Representation direction

Current `StreamStep.Next(value, continuation)` has no explicit close operation. This makes early termination depend on
the surrounding runtime scope rather than the stream execution that acquired the resource.

The implementation should represent an execution as an acquired, closeable pull cursor. Exact internal types remain an
implementation detail, but the conceptual model is:

```fsharp
type StreamStep<'value> =
    | Done
    | Yield of 'value

type StreamCursor<'error, 'value> =
    internal
        { Pull: unit -> Execution<StreamStep<'value>, 'error>
          Close: unit -> Execution<unit, Never> }

type FlowStream<'env, 'error, 'value> =
    internal FlowStream of Flow<'env, 'error, StreamCursor<'error, 'value>>
```

This sketch does not require exposing cursor types. It requires these behaviors:

1. Opening a stream is a Flow acquisition.
2. Terminal consumers open a child scope.
3. Cursor-owned resources register in that child scope.
4. Operators wrap cursors and propagate closure upstream.
5. Terminal consumers close the child scope in every exit path.

If this can be achieved with a different internal representation while preserving those laws, prefer the simpler
implementation.

## Construction API

Prefer a small set of deep constructors:

```fsharp
FlowStream.empty
FlowStream.singleton
FlowStream.fromSeq
FlowStream.fromFlow
FlowStream.unfoldFlow
FlowStream.acquireRelease
```

`FlowStream.acquireRelease` is the resource-owning foundation. It should delegate acquisition and release to Flow
primitives rather than reproduce them. Its design must make state ownership and pull state explicit without exposing
the cursor representation.

A conceptual call shape is:

```fsharp
FlowStream.acquireRelease
    acquire
    release
    initialState
    pull
```

where:

- `acquire` is `Flow<'env, 'error, 'resource>`;
- `release` uses the same cleanup contract as `Flow.acquireRelease`;
- `pull` returns either one value and next state or completion;
- release runs exactly once even when acquisition succeeds but the first pull never completes.

Avoid overlapping public constructors named `using`, `bracket`, `resource`, and `scoped`. One regular name matching
Flow is easier to find and generate correctly. Additional private helpers may serve implementation needs.

## Operator rules

Every operator must declare and test three things: upstream pull behavior, upstream close behavior, and retained state.

### Stateless one-to-one operators

`map`, `mapError`, and `tapFlow`:

- acquire one upstream cursor;
- pull upstream once for each downstream pull;
- forward completion and failure;
- close upstream exactly once;
- retain at most one current value.

### Skipping operators

`filter`, `choose`, `skip`, and `skipWhile` may pull upstream multiple times for one downstream value. They must remain
stack-safe when millions of values are skipped synchronously.

### Early-ending operators

`take` and `takeWhile` close upstream immediately when their condition ends the downstream stream. They do not wait for
the enclosing application scope.

### Combining operators

- `append` closes the left cursor before acquiring or consuming the right cursor.
- `collect` gives every inner stream its own child scope and closes it before advancing the outer stream.
- `zip` closes both cursors when either side completes or fails.
- Any future `merge` must define fairness, failure, cancellation, ordering, and a strict buffer bound before becoming
  public.

### Terminal consumers

Keep names regular and encode cardinality explicitly:

```fsharp
FlowStream.runDrain
FlowStream.runForEach
FlowStream.runForEachFlow
FlowStream.runFold
FlowStream.runCollect
FlowStream.runHead
FlowStream.runTryHead
FlowStream.runExactlyOne
FlowStream.runTryExactlyOne
```

`runCollect` is intentionally unbounded because its name promises materialization. Documentation and XML comments must
state this. Streaming examples should not use it for long-lived sources.

`runHead` and `runTryHead` close upstream immediately after deciding their result. `runExactlyOne` pulls enough to
prove cardinality, then closes upstream.

### Bounded parallel consumption

Network servers and worker streams need one bounded terminal consumer, tentatively:

```fsharp
FlowStream.runForEachFlowPar
    (Parallelism.bounded 32)
    handle
    stream
```

Required behavior:

- never run more than the configured number of handlers;
- stop pulling while all slots are occupied;
- run every handler in its own child scope;
- cancel siblings and upstream when one handler fails;
- wait for child cleanup before returning;
- preserve typed failure without nondeterministically discarding additional defects;
- reject non-positive bounds at specification construction time;
- never use an unbounded queue.

Keep this internal until TCP serving or another concrete consumer proves the failure and ordering semantics. Do not add
general `flatMapPar`, `mergeAll`, or scheduler controls preemptively.

### Time policies

Potential operations are:

```fsharp
FlowStream.timeout
FlowStream.timeoutBetween
```

`timeout` limits complete stream consumption. `timeoutBetween` limits time waiting for the next value. Both should be
implemented using Flow runtime timing and should take an explicit typed error value. Do not add them until a proving
slice requires both semantics and tests distinguish them.

## Byte ownership

FlowStream remains generic and does not define a universal byte container.

Safe transport defaults:

- caller-owned mutable memory for primitive reads;
- read-only memory for primitive writes;
- owned `byte array` for emitted binary frames;
- `string` for emitted text frames.

Internal implementations may use pooled or segmented buffers. A frame is copied once into owned memory when emitted.
This permits retention, mapping, batching, and asynchronous handling without temporal ownership rules.

Do not expose pooled `ReadOnlyMemory<byte>` values whose validity ends on the next pull. Such a contract is easy for
humans to violate and especially likely for generated code to violate. A future zero-copy API must use an explicitly
disposable lease type, live in an advanced qualified module, and be justified by benchmarks.

## Proving slices

These slices exist to pressure different FlowStream behaviors. They need not provide complete public packages before
1.0.

### Process

Use existing process execution to prove:

- multiple native output producers feeding one consumer;
- slow-consumer backpressure;
- cancellation while a process or read is blocked;
- early consumer termination killing or detaching according to explicit ownership;
- final structured completion;
- stdout/stderr attribution;
- exactly-once process-tree cleanup.

Where possible, replace private lifecycle and rendezvous behavior with proven Flow/FlowStream primitives. Do not weaken
Process's application-level transcript or topology model merely to share code.

### TCP client

Implement only enough to prove:

- scoped connection acquisition;
- caller-buffer reads and read-only-memory writes;
- concurrent one-reader/one-writer operation;
- EOF and half-close;
- fragmented and coalesced frames;
- blocked read cancellation;
- early receive-stream termination;
- connection cleanup.

### Serial

Implement a narrow port-open and byte-duplex slice to prove:

- slow and irregular fragmentation;
- no-data periods;
- device disconnect during read or write;
- delimiter framing;
- cancellation where native serial APIs behave differently across platforms;
- cleanup after partial open.

CI should use a scripted fake on every platform and a pseudo-terminal integration test where supported. Physical
hardware is useful evidence but cannot be the only acceptance test.

### WebSocket

Implement a client slice sufficient for an OCPP-style session:

- text and binary messages;
- one receive stream plus serialized sends;
- native fragmentation hidden below complete message emission;
- close handshake;
- remote close and protocol failure;
- heartbeat implemented as ordinary Flow policy;
- cancellation and reconnect outside the connection's native interpreter.

WebSocket proves message streams rather than byte framing.

### Streaming HTTP and SSE

Implement streamed response bodies and an SSE decoder to prove:

- response resource ownership;
- headers available before body completion;
- one-way long-lived streams;
- incremental UTF-8 decoding;
- event fields spanning arbitrary chunks;
- comments and heartbeats;
- early body abandonment;
- browser `fetch`/`AbortSignal` behavior under Fable.

## Test model

### Scripted source

Create a platform-neutral test source capable of scripting:

- acquisition success or typed failure;
- synchronous and asynchronous values;
- completion;
- typed pull failure;
- blocking until cancellation;
- defect during pull;
- release success or defect;
- counters for opens, pulls, closes, and pulls-after-close;
- detection of concurrent pulls;
- configurable slow producer and slow consumer coordination.

### Lifecycle laws

Test every relevant constructor, operator, and terminal consumer for:

- zero acquisition before execution;
- one acquisition per execution;
- exactly one close after successful acquisition;
- no close when acquisition fails before ownership exists;
- close after normal completion;
- close after typed failure;
- close after interruption;
- close after downstream action failure;
- immediate close after early termination;
- no pull after `Done`;
- no pull after close;
- cleanup in reverse acquisition order;
- cleanup defect visibility consistent with Flow scopes.

### Stream laws

Where equality and finite inputs permit:

- `map id` preserves values, errors, and pull count;
- `map (f >> g)` matches `map f >> map g`;
- `filter (fun _ -> true)` preserves values;
- `take 0` acquires only if documented and never pulls;
- `append empty source` and `append source empty` preserve values;
- `runFold` matches the equivalent list fold;
- synchronous runs with millions of skipped or mapped values remain stack-safe.

Lifecycle observations are part of equivalence; matching values while leaking a resource is a failed law.

### Hostile scheduling tests

Cover cancellation and completion at every boundary:

- before acquisition;
- during acquisition;
- after acquisition before first pull;
- while pull is blocked;
- after a value is produced but before consumer handling completes;
- while cleanup is running;
- while a parallel child is starting or stopping.

Use deterministic coordination primitives rather than timing-dependent sleeps.

### Memory and throughput

Bench and stress:

- millions of synchronous values;
- one-byte fragments;
- many frames in one read;
- slow consumers;
- large allowed frames;
- rejected oversized frames;
- repeated connection/open/close cycles;
- cancellation under load;
- bounded parallel handlers at capacity.

Acceptance should establish bounded retention and absence of obvious regressions, not promise universal benchmark
leadership.

## Platform proving

### .NET

- Child process integration for Process.
- Loopback TCP integration without external network access.
- Local WebSocket peer in tests.
- `System.IO.Stream` adapter tests.
- `IAsyncEnumerable` adapter tests if that adapter becomes public.
- Linux pseudo-terminal Serial tests where CI permits.

### Fable browser

- Promise scheduling does not cause recursive stack growth.
- Browser WebSocket messages and close events map to the common semantics.
- Streaming `fetch` bodies release their reader lock after completion, error, cancellation, and early termination.
- `AbortSignal` correctly interrupts pending reads.
- Unsupported transports are absent rather than implemented as runtime failure stubs.

### Fable Node

- Node stream adapters pause/resume according to downstream demand.
- Event listeners are removed on every exit path.
- Buffered event count remains bounded.

### Future runtimes

Python, Rust, and Erlang are not implementation targets for this milestone. The design remains open to them by keeping
the required runtime kernel small:

- execute Flow;
- create and close child scopes;
- register finalizers;
- propagate interruption;
- pull one stream step.

Do not introduce a .NET-specific core optimization that changes public semantics or makes this kernel insufficient
without documenting and accepting that tradeoff.

## Naming and authoring guardrails

- Use `FlowStream` consistently; do not introduce `AsyncStream`, `TaskStream`, or carrier-specific workflow types.
- Use `run...` for terminal consumers.
- Use `from...` for adapters from existing values or platform abstractions.
- Use `to...` only for adapters that return another abstraction without executing it immediately.
- Use `map...` for one-to-one transformation and `choose...` for optional transformation.
- Match Flow names for shared concepts: `acquireRelease`, `scoped`, `mapError`, and `timeout`.
- Prefer qualified module functions over overload forests, optional parameters, SRTP, or implicit conversions.
- Put required arguments first and the stream last so pipeline use is regular.
- Reject invalid static configuration immediately with `invalidArg`; represent operational failure in the typed error.
- Keep common examples on the safe scoped path. Advanced constructors and platform escape hatches remain qualified.

## Work sequence

1. Record current FlowStream public API and behavioral tests.
2. Decide cross-Flow cancellation semantics, including existing Http and Process `Canceled` cases.
3. Prototype cursor/scoped representation without expanding the public operator catalog.
4. Port existing operators and add lifecycle-law tests for each.
5. Add the resource-aware construction primitive using Flow scopes and `Flow.acquireRelease`.
6. Prove early termination, failure, interruption, cleanup defects, and stack safety.
7. Migrate or adapt Process streaming to exercise the new model.
8. Build pure framing and the narrow TCP and Serial proving slices.
9. Build WebSocket message and streaming HTTP/SSE proving slices on .NET and Fable.
10. Add only operators demanded by at least one proving slice.
11. Run stress, allocation, and cancellation tests.
12. Freeze the 1.0 FlowStream semantic contract and fold it into `dev-docs/PLAN.md`.

## 1.0 exit gates

FlowStream is ready to freeze when:

- resource ownership and early-finalization semantics are documented and tested;
- all existing operators obey the lifecycle laws;
- long synchronous streams are stack-safe;
- Process passes integration tests on the new or proven-compatible implementation;
- shared framing passes fragmentation and bounded-memory tests;
- TCP and Serial proving slices exercise byte-duplex streaming;
- WebSocket proves message streaming through an OCPP-shaped session;
- SSE proves long-lived one-way streaming and incremental decoding;
- .NET and Fable adapters prove cancellation and cleanup;
- no proving slice needed a second scope, cancellation, timeout, or retry runtime beside Flow;
- any intentionally deferred operator or platform limitation is explicit.

Complete Network, Serial, WebSocket, SSE, and Compression feature sets are post-1.0 work described separately. Their
pre-1.0 slices exist to validate FlowStream, not to force unfinished satellite APIs into a stable release.
