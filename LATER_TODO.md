# Axial Flow-Group and Platform Backlog

This is the Flow-group and platform-track backlog, kept demand-driven per the release strategy in `prd.md`: the
boundary stack (`dev-docs/TASKS.md`) is the 1.0 gate, and items here are pulled forward when a concrete application
needs them, not worked top to bottom. Section numbering and the v1.0/post-v1.0 labels are preserved from the original
ZIO-delta roadmap for continuity; "v1.0" sections describe what a full Flow 1.0 would require, not the current gate.
One ordering rule survives from the original PRD: runtime foundation before broad module expansion — rich
`Cause`/`Exit`, fiber identity, scope, and environment/layer semantics (sections 3–4, done) stay prerequisites for
faithful concurrency, STM, streams, metrics, and tracing work.

It is .NET focused. JavaScript means Fable-generated JavaScript. JVM, JS, and Native reference material should be used as behavioral and compatibility input only, not as target platforms for .NET.

## References

- Product PRD: `prd.md`
- Reference PRD: external ZIO reference corpus, `specs/prd.md`
- Reference TODO: external ZIO reference corpus, `TODO.md`
- Reference source/test specs: external ZIO reference corpus, `specs/*.md`

## 1. Lock the Current Baseline

- [x] Run the current Axial test suite and record the baseline command/result in this repo.
- [x] Record a source/test inventory in CI or docs so future delta reviews can prove every `src/**/*.fs`, `src/**/*.fsproj`, `tests/**/*.fs`, and `tests/**/*.fsproj` file is covered.
- [x] Add API-shape tests for the public `Flow`, builder, validation, schedule, stream, STM, and service modules.
- [x] Add a Fable compilation gate for the intended JavaScript surface.
- [x] Add a .NET trimming/NativeAOT compatibility gate for the `net8.0` target.
- [x] Keep root docs as `prd.md` and `TODO.md` unless the repository adopts another naming convention.

## 2. v1.0 Core Scope

- [ ] Freeze the stable v1.0 `Flow<'env,'error,'value>` surface: constructors, execution, map/bind/fold, typed recovery, environment access, and `Result`/`Async`/`Task`/`ValueTask` interop.
- [ ] Preserve remaining builder, validation, diagnostics, service, hosting, and telemetry APIs unless a specific breaking change is approved.
- [x] Complete the approved Check/Take/BindError redesign: remove Guard, rename the old ok/fail-style checks to regular predicates, keep `Check.withError`, and regenerate tests/reference/guides.
- [x] Complete the 'continued' Check/Take/BindError redesign: collapse Take into Check, use when/take variants, and regenerate tests/reference/guides.
- [x] Add a v1.0 API baseline and changelog policy.

## 3. v1.0 Runtime Semantics

- [x] Expand `Cause<'error>` beyond `Fail | Die | Interrupt` with sequential/parallel composition, defect accumulation, trace attachment, and pretty printing.
- [x] Expand `Exit<'value,'error>` helpers to preserve full cause information across conversions.
- [x] Add fiber identity and metadata: `FiberId`, fiber start time, parent/child relationship, status, and diagnostic dump shape.
- [x] Document exact outcomes for cancellation token cancellation, `OperationCanceledException`, `Cause.Interrupt`, timeout, failed parallel branches, defects, and finalizer failures.
- [x] Decide not to add masking/unmasking APIs for v1; rely on scoped finalizer guarantees and cause composition.
- [x] Add tests for nested fibers, parent cancellation propagation, interrupted joins, and finalizer ordering under cancellation.

## 4. v1.0 Environment, Layers, and Scope

- [x] Remove the registry-backed runtime dependency model.
- [x] Replace `Flow.service` and `Flow.inject` with `Service<'service>.get()` and `Service<'service>.resolve()`.
- [x] Make `Scope`, `Layer`, and `Flow.provide` part of the public service provisioning model.
- [x] Move former ambient operational services to explicit services.
- [x] Add `BaseRuntime` and live/provider-backed layer helpers in `Axial.Flow.PlatformService`.
- [x] Align Console, FileSystem, Http, and Process packages to the service-plus-layer model.
- [x] Add public docs for explicit services, provider boundaries, layers, scopes/resources, and base runtime construction.
- [x] Update `llms.txt`, agent guidance, and generated API reference docs for the service/layer model.
- [x] Add v1 Layer composition ergonomics: child scopes, `mapError`, sequential `zip`, parallel `zipPar` / `merge`, `map2`, `map3`, and `layer { let! / and! }`.
- [x] Decide not to add tagged services or automatic `IHas<'service>` layer merging for v1; use explicit records and nominal contracts.
- [x] Add scoped resource helpers for local acquire/use/release, runtime-scope acquisition, layer-scope acquisition, finalizer ordering, and release on success/failure/defect/interruption.
- [x] Validate integration with `Microsoft.Extensions.DependencyInjection` and existing service packages.
- [x] Document intentional limitations in layer error typing, provider-backed startup validation, and scope ownership.

## 5. v1.0 Concurrency and State

- [x] Add Promise/deferred result primitive.
- [ ] Design bounded and unbounded queues only when a concrete v1 feature needs Axial-owned backpressure, shutdown, and interruption semantics.
- [x] Add semaphore primitive.
- [x] Add a minimal atomic `Ref` implementation with `make`, `get`, `set`, `update`, and `modify`, backed by a lock and covered by state tests and public docs.
- [ ] Stabilize `Ref` for v1.0 by adding and testing the remaining atomic `getAndSet` and `updateAndGet` helpers and confirming the API/semantics are sufficient for realistic concurrent applications.
- [ ] Cover future queue shutdown/fairness semantics and remaining state/resource cleanup cases in tests.

## 6. v1.0 Scheduling and Time

- [x] Add a minimal schedule implementation with recurrence limits, fixed spacing, exponential backoff, fixed-range jitter, attempt/delay outputs, and retry/repeat integration.
- [ ] Make schedules production-ready with schedule composition, configurable/deterministic jitter, elapsed outputs, reset behavior, overflow/invalid-delay handling, and any additional retry/repeat semantics required by realistic applications.
- [ ] Remove `Unchecked.defaultof<'error>` from `Schedule.repeat` when schedule evaluation or sleeping is interrupted, preserving interruption/cause information without fabricating a typed error.
- [ ] Add deterministic clock-driven tests for retry, repeat, timeout, and sleep.

## 7. v1.0 Observability

- [x] Add scoped runtime annotations, trace IDs, annotation sinks, and request/correlation/tenant metadata propagation into telemetry spans.
- [x] Add a minimal `ActivitySource` telemetry wrapper that creates spans and tags environment metadata plus existing and dynamically-added runtime annotations.
- [ ] Make the `ActivitySource` wrapper production-ready with exit tagging, typed-error and defect recording, cancellation tagging, activity status, and available fiber/runtime metadata.
- [ ] Integrate observability with `Axial.Flow.Telemetry` and `Microsoft.Extensions.Logging`.
- [ ] Add tests for annotation/span propagation through flows, fibers, layers, resources, and retries.

## 7a. Future Service Packages

- [ ] Treat service packages as explicit service contracts over the expected .NET API surface: wrap most operations a competent .NET developer would look for, omitting only obsolete, legacy-only, redundant, unsafe-to-abstract, or poor-Axial-fit APIs.
- [x] Use `Axial.Flow.PlatformService` and `Axial.Flow.FileSystem` as the first examples of near-complete service surfaces with live implementations, typed Flow helpers, fake-friendly contracts, tests, and generated reference docs.
- [ ] Expand `Axial.Flow.Console` into a near-complete console/terminal service package rather than only read/write-line helpers.
- [x] Expand `Axial.Flow.Http` into a practical HTTP service package covering common requests/responses, headers, text/JSON/byte content, per-request timeout, cancellation, error classification, host-owned `HttpClient` configuration, live tests, and user guides.
- [x] Expand `Axial.Flow.Process` into a practical process service package covering commands and pipelines, environment and working-directory configuration, structured and streaming output, cancellation, exit handling, typed errors, live tests, scripts, and user guides.
- [ ] Add explicit process timeout/deadline configuration and tests so callers do not have to arrange timeout cancellation outside `Axial.Flow.Process`.
- [ ] Design `Axial.Flow.Network` after the core v1 service/layer surface is stable.
- [ ] Decide whether telemetry needs explicit service contracts under a future telemetry package, or should remain runtime instrumentation through `Axial.Flow.Telemetry`.
- [ ] If telemetry services are introduced, define how they compose with annotations, `ActivitySource`, `Microsoft.Extensions.Logging`, layers, and host-provider boundaries.

## 8. v1.0 Compatibility Tracks

- [ ] Define the supported Fable subset explicitly.
- [x] Add a runnable Fable JavaScript project/gate that references Axial, compiles the intended surface, executes a codec round trip under Node, and checks that .NET-only APIs do not leak into the output (`benchmarks/Axial.Benchmarks.Fable` via `scripts/check-fable-js-surface.sh`).
- [ ] Audit APIs guarded by `#if !FABLE_COMPILER`, especially `AsyncAdapter`, `TaskAdapter`, `Ref`, `STM`, `Stream`, `Schedule`, `Task`, `ValueTask`, process, live console, filesystem, hosting, and telemetry APIs.
- [ ] Provide Fable-safe alternatives or document unsupported modules.
- [ ] Add trimming analyzer warnings as build failures for the `net8.0` target.
- [x] Add and run a small NativeAOT sample in CI that exercises core `Flow`, explicit services/layers, validation, resources, and host-provider boundaries (`examples/Axial.AotProbe`).
- [ ] Audit reflection usage in builders, tests, hosting, telemetry, and DI integration.
- [ ] Document unsupported dynamic patterns where trimming cannot be made safe.

## 9. v1.0 Documentation and Examples

- [ ] Update README/docs to explain Axial's target model: .NET baseline, Fable JavaScript, NativeAOT/trimming.
- [x] Add user-facing examples and guides for environments/layers, resource safety, fibers, cancellation and execution outcomes, retry/timeout schedules, validation, explicit services, provider boundaries, and base-runtime construction.
- [ ] Add or expand a realistic hosting example that ties those pieces together in an end-user application; topic guides alone do not complete the hosting example requirement.
- [ ] Add migration notes as runtime semantics become richer.
- [ ] Keep links back to the external ZIO reference corpus for parity rationale, not platform promises.

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
