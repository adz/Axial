# Release Notes

## Unreleased

- `schemagen` generates whole version chains: a `.contract` file may declare several versions of one contract (oldest first, contiguous). Superseded versions emit frozen version-suffixed types and modules (`ConfigV1`), the latest keeps the bare name, and its module gains a `contract` builder that takes each typed n-1 -> n migration as a parameter plus the `VersionSource` — migrations stay hand-written F# and the compiler enforces the chain.
- Added the Versioned Contracts guide (`docs/schema/contracts.md`): the `Contract<'model>` versioning engine, the `.contract` grammar by example, `schemagen` usage with `--check` drift guarding in CI, and the wire-tier-only positioning.

- Added `Axial.Flow.Telemetry.JavaScript`: OpenTelemetry tracing for Axial workflows compiled with Fable (Node and browser). `Otel.install` takes a host-supplied `@opentelemetry/api` object through structural bindings; `Otel.trace`/`traceWith` mirror the .NET `Activity.trace` span semantics and tag vocabulary, and `FiberTelemetry.observe`/`observeWithSpans` mirror the fiber observers. JavaScript targets only; the .NET build is inert.

- Renamed the fail-fast package from `Axial.Result` to `Axial.ErrorHandling`, keeping `Check`, focused `Result` helpers, collection traversal, and `result {}` together.
- Added `Axial.Refined` for parse helpers, initial refined value types, and `refine {}`.
- Added environment-aware `Policy` helpers and `Flow.verify` for running pure checks at workflow boundaries.
- Added `Axial.Schema`: portable `Schema<'model>` metadata authored with the progressive typed builder (`Schema.recordFor ... |> Schema.field ... |> Schema.build`), primitive field shorthands, refined/domain value schemas via `Value.refined`, nested models, collections, formats, and inspectable `SchemaConstraint` metadata.
- Added the public `Inspect` API (`Inspect.model`, `Inspect.value`, `Inspect.field`) describing built schemas as metadata trees for JSON Schema, documentation, and UI interpreters without running validation.
- Added `Axial.Validation.Schema`: source-agnostic `RawInput` with adapters (map, name/value, CLI args, JSON-like, configuration), `Input.parse` into `ParsedInput` with raw redisplay and field error lookup, `SchemaError` diagnostics, constructor-level intrinsic errors, `Validation.validate` for existing models, and contextual `RuleSet`/`Rules.apply` over already-trusted models.
- Added a Schema docs section (trusted construction, choosing a tool, refined values, redisplay and field errors, rules and policies, input sources) and a runnable policy example.
- Added `Axial.Codec`: compiled, reflection-free JSON codecs over built model schemas. `Json.compile` walks the schema's retained typed field chain into constructor-specialized encode/decode plans (cached wire-name bytes, typed field decoders, no `obj array` dispatch); `Json.serialize`/`serializeBytes`/`deserialize`/`deserializeBytes`/`tryDeserialize` run the trusted lane, and decode failures raise path-aware `JsonCodecException`.
- Promoted JSON Schema generation into `Axial.Schema` as `JsonSchema.generate`/`generateValue`, lowering shapes, formats, and constraint metadata to JSON Schema keywords, with tagged unions as `oneOf` and `const` discriminators.
- Added `RawInput.ofJsonElement` and `RawInput.ofJsonDocument` on .NET 8+ targets for adapting System.Text.Json bodies into schema input parsing.
- Added the runnable ASP.NET Core minimal-API sample (`examples/Axial.Api`): one schema declaration drives JSON parsing with 400 path diagnostics, codec responses, a generated OpenAPI document, and an HTML form with error redisplay; CI smoke-runs it.
- Added codec benchmarks against `System.Text.Json` plus a trusted-lane vs boundary-lane comparison, recorded in the benchmarks page.
- Renamed `Policy.pure` to `Policy.lift` so the recommended surface needs no double-backtick identifiers, and removed unnecessary backticks from `Value.int`/`Value.decimal`/`Value.bool` call sites in docs and samples.
- Added comparison docs (`vs FluentValidation`, `vs zod`, a sharpened FsToolkit.ErrorHandling page) and the public zero-reflection/AOT/trimming/Fable story page.

## 0.7.0 - 2026-06-21

- First public release under the `Axial` package and repository identity, replacing the previous `FsFlow` naming.
- Split the library into the coordinated Axial package family: `Axial.Flow`, `Axial.Result`, `Axial.Validation`, the umbrella `Axial` package, and focused `Axial.Flow.*` service packages.
- Made `Axial.Flow` the primary effect package for explicit environment, typed failure, async/task interop, runtime policy, layers, scoped cleanup, fibers, STM, streams, and scheduling.
- Added independent `Axial.Result` and `Axial.Validation` packages for fail-fast result helpers, `Check`, `result {}`, diagnostics, accumulating validation, and `validate {}`.
- Refreshed package metadata, README content, examples, generated reference pages, and documentation site content for the Axial identity and split package surface.
- Standardized pre-1.0 release versioning so every public Axial package in the release train ships at the same version from `Directory.Build.props`.

## 0.6.0 - 2026-05-17

- **Hybrid Interop Optimization**: Re-engineered the `flow {}` builder to use inlined overloads for `Task`, `ValueTask`, and `Async`. This eliminates the "adapter tax" and brings performance significantly closer to native `task {}` expressions.
- **Zero-Boilerplate Binding**: Directly `let!` and `return!` on any standard .NET asynchronous type without manual lifting or wrapping.
- **Improved Allocation Profile**: Reduced heap allocations by ~35% for mixed workflows interoperating with .NET tasks, while maintaining 100% runtime stability.
- **Refined Internal Architecture**: Optimized the unified `Flow` type for better cross-assembly inlining and Fable compatibility.
- **Design Decision Log**: Added formal documentation for the performance optimization strategy and deprecated outdated architectural records.

## 0.5.0 - 2026-05-17

- **Unified Flow Model**: Consolidated `AsyncFlow` and `TaskFlow` into a single, high-performance `Flow` type that works across all supported platforms (including Fable 5).
- **ZIO-Style Execution Semantics**: Introduced a robust `Exit` and `Cause` model that preserves the distinction between typed failures (`Fail`), cancellations (`Interrupt`), and unhandled defects (`Die`).
- **Structured Concurrency**: Added first-class support for fibers with `fork`, `join`, and `interrupt`, along with parallel orchestration primitives like `zipPar` and `race`.
- **Software Transactional Memory (STM)**: Implemented a composable STM engine with `TRef`, `retry`, `orElse`, and the `stm {}` computation expression for atomic state transitions.
- **Effectful Streams**: Introduced `FlowStream` with built-in backpressure and native `IAsyncEnumerable` interop for processing asynchronous data sequences.
- **Runtime Foundation**: Implemented a new internal `RuntimeRegistry` and `Scope` system for explicit service management and deterministic resource teardown.
- **Service Redesign**: Migrated to nominal service contracts using standard F# interfaces, making workflow signatures more readable and stable.
- **Fable 5 & Cross-Platform Support**: Full support for Fable 5 with a unified asynchronous strategy that remains performant on both .NET and JS targets.
- **Telemetry & Hosting**: Added hosting and telemetry packages for seamless DI integration, distributed tracing, and activity tagging.
- **Documentation Reorganization**: Completely restructured the documentation site with a hierarchical sidebar, new tutorials on dependency management, and a comprehensive API reference.

## 0.4.0 - 2026-05-03

- Introduced **Tuple-Based Smart Binds** in `flow {}`, `asyncFlow {}`, and `taskFlow {}` for a concise "unwrap or fail" DX
- Added `orFailTo` semantic label to clarify domain error attachment in smart binds
- Expanded `TaskFlow` smart binds to support `Task<Option<_>>`, `Task<Option<_>>`, `ValueTask<Option<_>>`, and `ValueTask<ValueOption<_>>`
- Major documentation overhaul with **function-level granularity** mirroring FsToolkit.ErrorHandling
- Enriched every public API member with detailed XML documentation (summary, remarks, parameters, returns)
- Added **expected output demonstrations** to validation and diagnostics guides
- New **"For AI Agents"** guide and machine-optimized `llms.txt` for better LLM assistance
- Improved site accessibility with better contrast and verified all documentation links

## 0.3.0 - 2026-05-02

- Major architectural shift to a workflow family: `Flow`, `AsyncFlow`, and `TaskFlow`
- Introduced a .NET task-oriented workflows and interop package
- Added `ColdTask<'value>` for deferred, restartable task factories
- Migrated documentation to a versioned Docusaurus site with generated runnable examples
- Reorganized the docs into a clearer product-manual path across getting started, execution semantics, runtime interop, environment slicing, and architecture
- Added package-oriented API landing pages
- Trimmed the README into a shorter NuGet-facing entry point
- Added pure validation helpers and effect bridges for `Async` and `Task`
- Expanded benchmark suite with BenchmarkDotNet and new comparison scenarios

## 0.2.0 - 2026-04-28

- Second public preview release
- Completed package and repository identity work across project files, examples, tests, docs, and packaging metadata
- Refreshed the docs site presentation and bundled docs assets for the renamed package
- Cleaned up solution and workflow references after the `v0.1.0` release
- Kept the public `Flow` API stable while polishing the package surface before larger follow-up changes

## 0.1.0 - 2026-04-26

- Initial public preview release
- Core `Flow<'env, 'error, 'value>` abstraction for explicit environment requirements, typed failures, and cold execution
- Direct `Result`, `Async`, `Task`, and `ColdTask` interop inside one `flow {}` workflow
- Runtime helpers for cancellation, timeout, retry, logging, and scoped cleanup
- User-facing guides for getting started, environment slicing, semantics, task and async interop, and supported architectural styles
- Runnable example applications plus a NativeAOT probe
- NuGet packaging metadata, symbols, SourceLink, and GitHub Pages API docs pipeline

## Release Process

Publish versions as Git tags such as `v0.7.0`.

The GitHub release workflow builds the package artifacts and attaches them to a GitHub Release.

NuGet publishing is handled by the release workflow for `v*.*.*` tags. Use `scripts/pack.sh` to build local artifacts before tagging.
