# FsFlow Delta PRD

## Purpose

This document records the delta between the current FsFlow implementation and the reference roadmap in `/home/adam/projects/zio_fsflow_docs/zio`. FsFlow is an F#/.NET effect library. The implementation target is .NET first, with JavaScript support through Fable-generated JavaScript. .NET does not target the JVM, and this roadmap must not imply a JVM runtime target.

The ZIO JVM/JS/Native files in `/home/adam/projects/zio_fsflow_docs/zio` are reference lenses for behavior, naming, constraints, and module shape. They are not platform commitments for FsFlow.

## Reference Inputs

- Reference PRD: `/home/adam/projects/zio_fsflow_docs/zio/specs/prd.md`
- Reference TODO: `/home/adam/projects/zio_fsflow_docs/zio/TODO.md`
- ZIO source/test specs: `/home/adam/projects/zio_fsflow_docs/zio/specs/*.md`
- FsFlow source: `/home/adam/projects/FsFlow/main/src`
- FsFlow tests: `/home/adam/projects/FsFlow/main/tests/FsFlow.Tests`

## Current FsFlow Product Shape

FsFlow already provides a compact typed effect model:

- `src/FsFlow/Core.fs` defines `Cause<'error>`, `Exit<'value,'error>`, `Flow<'env,'error,'value>`, `Fiber<'error,'value>`, runtime service contracts, retry policy, and .NET/Fable conditional effect representation.
- `src/FsFlow/Flow.fs` provides construction, execution, conversion to `Async`/`Task`/`ValueTask`, environment access, mapping/binding, typed recovery, `fork`/`join`/`interrupt`, `zipPar`, .NET `race`, timeouts, retry, sleep, logging, clock/random/GUID/environment-variable access, and acquire/release.
- `src/FsFlow/FlowBuilder.fs`, `BindError.fs`, `ResultBuilder.fs`, `ValidateBuilder.fs`, and `Builders.fs` provide computation expressions and interop for `Result`, `Async`, `Task`, `ValueTask`, `option`, `voption`, and flow bind-site error adaptation.
- `src/FsFlow/Runtime*.fs` provides internal registry, tagged service lookup, nominal runtime adaptation, simple layer composition, and deterministic scope finalization.
- `src/FsFlow/Ref.fs`, `Stm.fs`, `Stream.fs`, and `Schedule.fs` provide initial state, STM, streaming, and scheduling primitives on .NET.
- `src/FsFlow/Diagnostics.fs`, `Validation.fs`, and `Check.fs` provide structured validation diagnostics and applicative validation support.
- Capability packages cover core runtime services, console, filesystem, HTTP, and process effects.
- `src/FsFlow.Hosting/Hosting.fs` integrates with `Microsoft.Extensions.DependencyInjection` and logging.
- `src/FsFlow.Runtime.Telemetry/Telemetry.fs` adds activity-based telemetry helpers.

The main package currently targets `netstandard2.1` and `net8.0`, references `Fable.Core`, and marks the `net8.0` build as AOT compatible. The tests target `net10.0`.

## Source Inventory Covered

Core package files reviewed:

- `AssemblyInfo.fs`: assembly metadata.
- `Core.fs`, `Foundation.fs`, `Flow.fs`: the core `Flow` representation, effect helpers, execution, construction, environment access, runtime helpers, fibers, parallel composition, and transformations.
- `FlowBuilder.fs`, `Builders.fs`, `ResultBuilder.fs`, `ValidateBuilder.fs`: computation expressions and public builder values.
- `AsyncAdapter.fs`, `TaskAdapter.fs`: .NET-only adapter flow families for async/task-oriented composition.
- `Check.fs`, `BindError.fs`, `Diagnostics.fs`, `Validation.fs`: pure predicates, value-preserving gates, extracting checks, flow bind-site error adaptation, structured diagnostics, and accumulating validation.
- `Runtime.fs`, `RuntimeScope.fs`, `RuntimeRegistry.fs`, `RuntimeAdapter.fs`, `RuntimeLayer.fs`: ambient runtime services, service registry, scoped finalizers, adapter projection, and internal layer composition.
- `Ref.fs`, `Stm.fs`, `Stream.fs`, `Schedule.fs`: .NET-only state, transactional memory, stream, and schedule primitives.
- `FsFlow.fsproj`: `netstandard2.1;net8.0` target configuration, Fable dependency, and `net8.0` AOT compatibility marker.

Extension package files reviewed:

- `FsFlow.Capabilities.Core/Core.fs`: clock, log, random, GUID, environment variables, deterministic test providers, live providers, and typed environment variable parsing errors.
- `FsFlow.Capabilities.Console/Console.fs`: console read/write capability with live implementation guarded for non-Fable.
- `FsFlow.Capabilities.FileSystem/FileSystem.fs`: file read/write/exists capability with live filesystem implementation.
- `FsFlow.Capabilities.Http/Http.fs`: HTTP string fetch capability over `HttpClient`.
- `FsFlow.Capabilities.Process/Process.fs`: process execution capability with live implementation guarded for non-Fable.
- `FsFlow.Hosting/Hosting.fs`: DI/logging runtime creation and startup environment validation.
- `FsFlow.Runtime.Telemetry/Telemetry.fs`: `ActivitySource` tracing wrapper with request/correlation/tenant tags.
- Each extension `.fsproj`: package boundaries for the capability, hosting, and telemetry modules.

## Test Coverage Observed

The current tests exercise the library as a pragmatic .NET workflow toolkit:

- `TestSupport.fs`: shared domain fixtures, reflection helpers for builder overload checks, FSI/bash script runners, and single-consumption value-task source.
- `WorkflowBasicTests.fs`: constructors, combinators, delays, environment access, layers, DI injection, service access, traversal/sequence, builder overload shape, examples generation.
- `ExecutionTests.fs`: `Async`/`Task` result conversion and cancellation behavior.
- `WorkflowConcurrencyTests.fs`, `WorkflowParallelTests.fs`: fibers, interruption, parallel zip, and race-style behavior.
- `WorkflowErrorTests.fs`: typed failures, defects, option/value-option adapters, guard helpers, builder overload constraints, and error mapping.
- `WorkflowSchedulingTests.fs`: schedules, retry, timeout, and cancellation helpers.
- `WorkflowResourceTests.fs`: acquire/release cleanup on success and defects.
- `WorkflowStateTests.fs`: `Ref`, `STM.atomically`, `STM.retry`, and `STM.orElse`.
- `WorkflowStreamTests.fs`: minimal stream consumption and mapping.
- `ValidationTests.fs`: checks, result builder, diagnostics graph rendering/flattening, scoped validation, accumulation, and fallback helpers.
- `RuntimeFoundationTests.fs`: service registry, tagged lookup, scope finalizers, runtime adapter, ambient runtime overrides, and environment variable parsing.
- `CapsCoreTests.fs`, `CapsUnifiedTests.fs`, `CapsRuntimePatternTests.fs`, `HostingTests.fs`, `TelemetryTests.fs`: capability abstractions, hosting, runtime patterns, and telemetry.
- `FsFlow.Tests.fsproj`: `net10.0` test project referencing all FsFlow source, capability, hosting, and telemetry projects.

## Delta Against Reference Roadmap

FsFlow has a useful foundation but is not yet a full F#/.NET analogue of the referenced ZIO surface.

Implemented or partially implemented:

- Typed effect shape with environment, error, and success channels.
- Basic `Cause`/`Exit` model for expected failure, defect, and interruption.
- Runtime helpers for clock, logging, random, GUID, environment variables, sleep, timeout, retry, cancellation, and acquire/release.
- F# computation expression support and .NET interop with `Async`, `Task`, `ValueTask`, `Result`, `option`, and `voption`.
- Internal async/task adapter flow families for workflows that need adapter-specific composition before returning to `Flow`.
- Basic fibers with cooperative cancellation.
- Basic parallel composition through `zipPar` and .NET `race`.
- Minimal runtime registry, layer, adapter, and scope internals.
- Basic `Ref`, `STM`, `FlowStream`, and `Schedule`.
- Structured validation and diagnostics.
- Capability packages, hosting integration, and `ActivitySource` telemetry.

Major gaps:

- No rich fiber runtime: missing `FiberId`, fiber status, fiber refs, supervision, runtime flags, execution strategy, interruption status, structured fiber dumps, and full cooperative interruption semantics.
- `Cause` is minimal: missing parallel/sequential cause composition, trace attachment, pretty printing, defect accumulation, and stronger conversions.
- Environment/layer support is small: no public `ZEnvironment`-like typed environment, no full `ZLayer` graph composition, memoization, reloadable services, or scope-safe resource graph.
- Scope/resource management is internal and simple: no public managed resource abstraction equivalent to `ZManaged`, no release map, no finalizer exit semantics, and no scope hierarchy.
- STM is minimal: missing ZIO-style `TArray`, `TMap`, `TQueue`, `THub`, `TPromise`, `TPriorityQueue`, `TRandom`, `TReentrantLock`, `TSemaphore`, `TSet`, advanced retry coordination, and richer transactional combinators.
- Streams are minimal: missing `ZChannel`, `ZSink`, `ZPipeline`, `ZStream` constructors/operators, `Take`, subscription refs, chunked pull model, buffering, async boundaries, encoding/compression, resource/file/socket constructors, and test-kit style sinks.
- Concurrency primitives are sparse: missing queues, hubs, latches, barriers, promises, semaphores, async locks, reentrant locks, and concurrent maps/sets.
- Observability is initial only: missing log annotations/spans/aspects, metrics labels/listeners/connectors, tracer model, parsed stack traces, and source locations.
- Collections and internal infrastructure are absent or thin: missing chunk/non-empty chunk, differ/patch support, ring buffers, versioned/atomic hub strategies, and reloadable references.
- Fable support exists as conditional compilation and API shape pressure, but needs explicit Fable build/test gates for the intended JavaScript output.
- Several live capabilities and modules are currently non-Fable only or partially unavailable under Fable, including process, live console, `Ref`, STM, stream, schedule, and adapter paths that depend on .NET task/value-task APIs.
- AOT/trimming support is marked for `net8.0` but needs analyzer gates, compatibility tests, and reflection review.

## v1.0 Release Scope

FsFlow v1.0 should not attempt full ZIO parity. The first stable release should be a coherent F#/.NET effect library with production-grade typed errors, resource safety, cancellation semantics, dependency ergonomics, and honest compatibility boundaries.

Required for v1.0:

- Stable `Flow<'env,'error,'value>` API with construction, map/bind/fold, typed recovery, environment access, and `Result`/`Async`/`Task`/`ValueTask` interop.
- `Cause` and `Exit` rich enough for production debugging: typed failure, defect, interrupt, sequential/parallel composition, readable rendering, and lossless conversion where possible.
- Defined interruption semantics for cancellation tokens, fibers, `fork`/`join`/`interrupt`, `zipPar`, `race`, timeouts, resource finalizers, and parallel failure/interruption composition.
- Public scoped resource model for acquire/use/release, finalizer ordering, and release on success, typed failure, defect, and interruption.
- Runtime services for clock, logging, random, GUID, and environment variables, with deterministic test providers and local overrides.
- Environment/layer story that fits F# records/interfaces and .NET DI without requiring a full clone of ZIO's layer machinery.
- Existing validation, check, diagnostics, guard, and builder ergonomics preserved and documented.
- Scheduling basics for retry, repeat, timeout, spaced/exponential/jittered schedules, and deterministic clock-driven tests.
- Core concurrency basics: `Ref`, promise/deferred, semaphore, and bounded/unbounded queue. Hubs, barriers, reentrant locks, and concurrent maps/sets may wait unless needed by the v1.0 runtime.
- Observability basics through structured logging context and `ActivitySource` integration with success/failure/defect/interruption tags.
- Compatibility gates for .NET tests, Fable compilation of the supported JavaScript surface, and trimming/NativeAOT smoke coverage for the .NET deployment surface.
- Documentation and examples showing dependency access, resource safety, cancellation, parallel composition, retry/timeout, validation, hosting, and compatibility limits.

Explicitly post-v1.0 unless a concrete application need pulls them forward:

- Full STM ecosystem beyond the minimal v1.0 state/concurrency need.
- Full stream/channel/sink/pipeline stack.
- Full fiber runtime with supervisors, dumps, runtime flags, detailed fiber refs, and execution strategies.
- Macro/source-generator parity for accessor and reloadable service APIs.
- Metrics subsystem beyond basic logging/activity integration.
- Complete ZIO API parity.

## Product Requirements

1. Keep FsFlow .NET-first.
   - Primary runtime targets should remain .NET TFMs.
   - JavaScript support must mean Fable-generated JavaScript.
   - JVM and ZIO Native material should be cited only as reference semantics.

2. Preserve the current small, idiomatic F# surface while expanding capability.
   - Existing `Flow<'env,'error,'value>` workflows and computation expressions must remain source compatible unless a breaking change is explicitly accepted.
   - Add richer runtime features behind coherent F# APIs rather than copying Scala names mechanically where they hurt F# usage.

3. Build the runtime foundation before broad module expansion.
   - Rich `Cause`, `Exit`, fiber identity, fiber-local state, scope, and environment/layer semantics are prerequisites for faithful concurrency, STM, streams, metrics, and tracing.

4. Treat .NET, Fable, and AOT as separate compatibility tracks.
   - .NET is the implementation baseline.
   - Fable is a compile/runtime compatibility track for JavaScript output.
   - NativeAOT/trimming is a .NET deployment compatibility track.

5. Expand tests from current user-facing workflows into parity-driven behavior suites.
   - Continue using the existing FsFlow tests as regression anchors.
   - Add focused tests for each reference-inspired feature, especially cancellation, finalization, concurrent races, stream resource safety, STM retry behavior, Fable compilation, and AOT/trimming.

## Non-Goals

- Do not target JVM from .NET.
- Do not promise direct execution on ZIO Native.
- Do not port Scala implementation details that are irrelevant to .NET, F#, Fable, or NativeAOT behavior.
- Do not replace FsFlow’s existing F#-friendly API with a mechanically translated Scala API.
