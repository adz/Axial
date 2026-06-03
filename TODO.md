# FsFlow TODO

This TODO is for FsFlow in `/home/adam/projects/FsFlow/main`. It is .NET focused. JavaScript means Fable-generated JavaScript. JVM, JS, and Native files in `/home/adam/projects/zio_fsflow_docs/zio` should be used as behavioral and compatibility references only, not as target platforms for .NET.

## References

- Delta PRD: `prd.md`
- Reference PRD: `/home/adam/projects/zio_fsflow_docs/zio/specs/prd.md`
- Reference TODO: `/home/adam/projects/zio_fsflow_docs/zio/TODO.md`
- Reference source/test specs: `/home/adam/projects/zio_fsflow_docs/zio/specs/*.md`

## 1. Lock the Current Baseline

- [ ] Run the current FsFlow test suite and record the baseline command/result in this repo.
- [ ] Record a source/test inventory in CI or docs so future delta reviews can prove every `src/**/*.fs`, `src/**/*.fsproj`, `tests/**/*.fs`, and `tests/**/*.fsproj` file is covered.
- [ ] Add API-shape tests for the public `Flow`, builder, validation, schedule, stream, STM, and service modules.
- [ ] Add a Fable compilation gate for the intended JavaScript surface.
- [ ] Add a .NET trimming/NativeAOT compatibility gate for the `net8.0` target.
- [ ] Keep root docs as `prd.md` and `TODO.md` unless the repository adopts another naming convention.

## 2. v1.0 Core Scope

- [ ] Freeze the stable v1.0 `Flow<'env,'error,'value>` surface: constructors, execution, map/bind/fold, typed recovery, environment access, and `Result`/`Async`/`Task`/`ValueTask` interop.
- [ ] Preserve existing builder, guard, validation, check, diagnostics, service, hosting, and telemetry APIs unless a specific breaking change is approved.
- [ ] Add a v1.0 API baseline and changelog policy.

## 3. v1.0 Runtime Semantics

- [ ] Expand `Cause<'error>` beyond `Fail | Die | Interrupt` with sequential/parallel composition, defect accumulation, trace attachment, and pretty printing.
- [ ] Expand `Exit<'value,'error>` helpers to preserve full cause information across conversions.
- [ ] Add fiber identity and metadata: `FiberId`, fiber start time, parent/child relationship, status, and diagnostic dump shape.
- [ ] Rework interruption semantics so cancellation, finalization, masking/unmasking, and interruption causes compose predictably.
- [ ] Add tests for nested fibers, failed joins, interrupted joins, parent cancellation, and finalizer ordering under cancellation.
- [ ] Document exact outcomes for cancellation token cancellation, `OperationCanceledException`, `Cause.Interrupt`, timeout, failed parallel branches, defects, and finalizer failures.

## 4. v1.0 Environment, Layers, and Scope

- [x] Remove the registry-backed runtime dependency model.
- [x] Replace `Flow.service` and `Flow.inject` with `Service<'service>.get()` and `Service<'service>.resolve()`.
- [x] Make `Scope`, `Layer`, and `Flow.provide` part of the public service provisioning model.
- [x] Move former ambient operational services to explicit services.
- [x] Add `BaseRuntime` and live/provider-backed layer helpers in `FsFlow.Services.Core`.
- [x] Align Console, FileSystem, Http, and Process packages to the service-plus-layer model.
- [x] Add public docs for explicit services, provider boundaries, layers, scopes/resources, and base runtime construction.
- [x] Update `llms.txt`, agent guidance, and generated API reference docs for the service/layer model.
- [x] Add v1 Layer composition ergonomics: child scopes, `mapError`, sequential `zip`, parallel `zipPar` / `merge`, `map2`, `map3`, and `layer { let! / and! }`.
- [ ] Decide whether FsFlow should add tagged services, or whether `IHas<'service>` plus explicit records is sufficient for v1.
- [x] Add scoped resource helpers for local acquire/use/release, runtime-scope acquisition, layer-scope acquisition, finalizer ordering, and release on success/failure/defect/interruption.
- [ ] Validate integration with `Microsoft.Extensions.DependencyInjection` and existing service packages.
- [x] Document intentional limitations in layer error typing, provider-backed startup validation, and scope ownership.

## 5. v1.0 Concurrency and State

- [ ] Add Promise/deferred result primitive.
- [ ] Add bounded and unbounded queues.
- [ ] Add semaphore primitive.
- [ ] Stabilize `Ref` for v1.0, including atomic modify/get-and-set/update-and-get helpers.
- [ ] Cover interruption, shutdown, fairness, and resource cleanup in tests.

## 6. v1.0 Scheduling and Time

- [ ] Expand schedules with composition, recurrence limits, jitter controls, elapsed/attempt outputs, and reset behavior needed for retry/repeat.
- [ ] Connect schedules to retry/repeat APIs without using default unchecked errors.
- [ ] Add deterministic clock-driven tests for retry, repeat, timeout, and sleep.

## 7. v1.0 Observability

- [ ] Add structured log context or annotations sufficient for request/correlation/tenant-style metadata.
- [ ] Extend the current `ActivitySource` telemetry wrapper with exit tagging, error/defect recording, cancellation tagging, and fiber/runtime metadata available in v1.0.
- [ ] Integrate observability with `FsFlow.Runtime.Telemetry` and `Microsoft.Extensions.Logging`.
- [ ] Add tests for annotation/span propagation through flows, fibers, layers, resources, and retries.

## 7a. Future Service Packages

- [ ] Design `FsFlow.Services.Network` after the core v1 service/layer surface is stable.
- [ ] Decide whether telemetry needs explicit service contracts under `FsFlow.Services.Telemetry`, or should remain runtime instrumentation through `FsFlow.Runtime.Telemetry`.
- [ ] If telemetry services are introduced, define how they compose with annotations, `ActivitySource`, `Microsoft.Extensions.Logging`, layers, and host-provider boundaries.

## 8. v1.0 Compatibility Tracks

- [ ] Define the supported Fable subset explicitly.
- [ ] Add a minimal Fable app/test project that references FsFlow and compiles to JavaScript.
- [ ] Audit APIs guarded by `#if !FABLE_COMPILER`, especially `AsyncAdapter`, `TaskAdapter`, `Ref`, `STM`, `Stream`, `Schedule`, `Task`, `ValueTask`, process, live console, filesystem, hosting, and telemetry APIs.
- [ ] Provide Fable-safe alternatives or document unsupported modules.
- [ ] Add trimming analyzer warnings as build failures for the `net8.0` target.
- [ ] Add a small NativeAOT sample that exercises core `Flow`, services, validation, resources, and hosting boundaries.
- [ ] Audit reflection usage in builders, tests, hosting, telemetry, and DI integration.
- [ ] Document unsupported dynamic patterns where trimming cannot be made safe.

## 9. v1.0 Documentation and Examples

- [ ] Update README/docs to explain FsFlow's target model: .NET baseline, Fable JavaScript, NativeAOT/trimming.
- [ ] Add examples for environment/layer usage, resource safety, fibers, cancellation, retry/timeout, validation, services, and hosting.
- [ ] Add migration notes as runtime semantics become richer.
- [ ] Keep links back to `/home/adam/projects/zio_fsflow_docs/zio` for parity rationale, not platform promises.

## 10. Post-v1.0 Fiber Runtime

- [ ] Add fiber-local state similar to FiberRef/FiberRefs, adapted to .NET/F#.
- [ ] Add supervision hooks for fiber start/end/failure/interruption.
- [ ] Add runtime flags and execution strategy where they materially affect .NET behavior.
- [ ] Add structured fiber dumps and richer runtime diagnostics.

## 11. Post-v1.0 STM Expansion

- [ ] Replace or extend the current global-lock STM with a design that can scale while preserving correctness.
- [ ] Add `TRef.modify`, `getAndSet`, `updateAndGet`, and common atomic helpers.
- [ ] Add transactional collections: array, map, set, queue, priority queue, hub, promise, random, semaphore, and reentrant lock.
- [ ] Strengthen `retry` and `orElse` semantics with tests for wakeups, lost notifications, and concurrent commits.
- [ ] Add STM interop tests with fibers and cancellation.

## 12. Post-v1.0 Streams, Sinks, Channels, and Pipelines

- [ ] Define a chunked pull model for `FlowStream` that works on .NET and can compile through Fable where supported.
- [ ] Add core stream constructors: succeed, fail, effect, unfold, repeat, range, async callback/queue-backed constructors.
- [ ] Add stream combinators: map, mapEffect, filter, take/drop, grouped/chunked, merge, zip, flatMap, retry, schedule, timeout, bracket/scoped resource handling.
- [ ] Add sinks for fold, collect, head, count, drain, foreach, and failure capture.
- [ ] Add pipelines for transformation, buffering, text encoding/decoding, and compression where .NET APIs are available.
- [ ] Add channel internals only when needed to support stream correctness and composition.
- [ ] Add stream tests for backpressure, interruption, finalizers, failures, chunk boundaries, and Fable-compatible subsets.

## 13. Post-v1.0 Collections and Internal Infrastructure

- [ ] Add chunk and non-empty chunk types if stream and queue work show a measurable need.
- [ ] Add differ/patch support if reloadable services or environment updates require it.
- [ ] Add internal ring-buffer and queue strategies for stream/hub performance.
- [ ] Keep these internals hidden until a public API has a clear user story.

## 14. Post-v1.0 ZIO Parity Work

- [ ] Add hubs/pub-sub where the .NET implementation can preserve backpressure and shutdown semantics.
- [ ] Add latch, barrier, async mutex, reentrant lock, and concurrent map/set helpers where they compose with `Flow` cancellation and failure.
- [ ] Add metrics primitives: counters, gauges, histograms, labels, listeners, and runtime connectors.
- [ ] Add tracing/source-location model adapted to F# and .NET stack traces.
- [ ] Add source-generator or build-time codegen equivalents for accessor and reloadable-service ergonomics.
- [ ] Use the local ZIO JS files only as behavior and platform-constraint references.
- [ ] Use the local ZIO Native files only as compatibility-lens references.
