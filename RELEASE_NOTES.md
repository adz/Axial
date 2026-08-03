# Release Notes

## Unreleased

### Meta-packages retired (breaking)

`Axial.ErrorHandling` and the `Axial` umbrella are removed. Neither carried API: `Axial.ErrorHandling` was
dependency-only, and `Axial` added a single re-export of the `result { }` builder on top of its three references.
Both package ids are retired with no replacement and no deprecation shim — this lands pre-1.0 and nothing released
depends on them.

**Migration:** install the focused packages you actually use.

| Before | Now |
| --- | --- |
| `dotnet add package Axial.ErrorHandling` | `Axial.Result`, `Axial.Constraint`, `Axial.Refined`, `Axial.Parse` — whichever you use |
| `dotnet add package Axial` | the focused packages above, plus `Axial.Schema` and/or `Axial.Flow` |
| `open Axial` for `result { }` | `open Axial.Result` |

`open Axial` remains valid where it means `Axial.Data`'s namespace; only the umbrella package's re-export is gone.

The former error-handling family survives as search vocabulary, not as a package: Result is presented as its own
product, and Constraint, Refined, and Parse under a **Values** navigation grouping with no package behind it.

### Constraint unification (breaking)

Value rules now have one vocabulary. `Axial.Check` is renamed **`Axial.Constraint`**, and `Constraint<'value>` — a
reusable description of valid values that `check` executes — replaces every parallel surface. Pre-1.0, so superseded
APIs are removed outright rather than deprecated.

**Removed, with no aliases:** the `Check<'value>` type, `CheckFailure` and its expectation types, `CheckDSL`, the
public `Predicate` catalogue, `Axial.Check.Constraint`'s code/metadata surface, `Axial.Schema.SchemaConstraint`,
`ConstraintDescriptor`, the `Axial.Schema.Constraint` facade and its duplicate catalogue in `Schema.Syntax`,
`Refinement.defineAll`/`defineWithCheck`, and per-constraint message overrides (`Constraint.withMessage`).

**Migration at a glance:**

| Before | Now |
| --- | --- |
| `open Axial.Check` | `open Axial.Constraint` |
| `Check.all [ ... ]` returning a function | `Constraint.all [ ... ]`, run with `Constraint.check` |
| `Check.String.present`, `Check.present` | `Constraint.present` (annotate the binding) |
| `Check.empty` / `Check.notEmpty` | `Constraint.blank` / `Constraint.minLength 1` |
| `Check.not` | `Constraint.notWith "<why>"` |
| `Constraint.define code args check` | `Constraint.custom "<why>" predicate` or `Constraint.customWith` |
| `Refinement.defineAll [ a; b ]` | `Refinement.define (Constraint.all [ a; b ])` |
| `Constraint.supplied` / `omittable` | `Schema.mustSupply` / `Schema.mayOmit`, or `mustSupply` in a field block |
| `constrain (fromCheck c)` | `constrain c` |
| `CheckFailure` in your error type | `Violation` |
| `CheckFailure.describeAll` | `Violation.render` |

**Behaviour changes worth reading before upgrading:**

- **Text sizes count Unicode code points**, not UTF-16 code units, so one emoji counts once. This departs from
  `String.Length`, and it is what lets `minLength`/`maxLength` lower to JSON Schema without over- or under-enforcing.
- **`Constraint.numeric` is ASCII** (`^[0-9]+$`). The old `\d` rule matched any Unicode decimal digit, which no
  ECMA-262 lowering could reproduce.
- **JSON Schema lowering is honest about what it enforces.** Text `present` now emits only `minLength: 1` and keeps
  the non-blank rule in `x-axial-runtime-constraints`, because .NET whitespace and ECMA-262 `\s` disagree in both
  directions. The email constraint lowers to its exact runtime `pattern`; the separate `SchemaFormat.email`
  annotation still lowers to `format: "email"`, and declaring both now emits both instead of one suppressing the
  other. Authored `Constraint.pattern`, IEEE-float relations, and GUID/instant equality are retained as runtime-only
  metadata rather than emitted as keywords that would mean something different at the other end.
- **Constraint failures are no longer lowered into parse-shaped `SchemaError` cases.** A read-then-rejected value is
  `SchemaError.Violation`, carrying the whole violation tree at its path. `SchemaError.Blank` keeps only its
  parse-side meaning. Several constraints on one field now produce one grouped violation rather than several
  diagnostics.
- **`Constraint.lessThan infinity` no longer throws.** Operand conversion happens at construction and never throws;
  floats keep their own representation instead of being forced through `decimal`.
- **Constructors that take an operand are now `inline`** (`equalTo`, `notEqualTo`, the four ordered comparisons,
  `between`, `oneOf`, `contains`, `distinct`, `multipleOf`). This is source-compatible for F# callers. It exists so
  the operand's portable form resolves on its static type: Fable erases a `Guid` to a plain string and a
  `TimeSpan` to a number, so a boxed type test described those operands as `Text` and `Integer` there while .NET
  described them correctly.

## 0.7.0 - 2026-07-28

First public release under the `Axial` name and repository identity (previously published as `FsFlow`). This
release settles the package family shape; treat it as the project's actual debut rather than an increment on
prior `FsFlow`/`Axial` previews. See the docs site for guides and API reference — these notes stay at the
package level.

- **`Axial.Flow`** — the effect and runtime package: explicit environments, typed failures, async/task/`ColdTask`
  interop, layers, scoped cleanup, fibers and structured concurrency, STM, streams, scheduling, and runtime
  policy. Companion service packages (`Axial.Flow.Console`, `.FileSystem`, `.HttpClient`, `.Process`,
  `.PlatformService`), hosting adapters (`Axial.Flow.Hosting`, `.Hosting.Node`, `.Hosting.Browser`), and
  telemetry (`Axial.Flow.Telemetry` for .NET, `Axial.Flow.Telemetry.JavaScript` for Fable) round out the runtime
  story with fiber diagnostics, a `FiberRegistry`, and OpenTelemetry integration on both platforms.
- **`Axial.ErrorHandling`** (the error-handling family) — fail-fast `Result` composition and `result {}`
  (`Axial.Result`), reusable value checks and predicates (`Axial.Constraint`), constraint-backed refined/domain types
  and `refine {}` (`Axial.Refined`), and primitive parsers for untrusted input (`Axial.Parse`), with
  `Axial.ErrorHandling` itself a dependency-only meta-package installing the core pieces together.
- **`Axial.Schema`** — portable `Schema<'model>` metadata for validation, codecs, documentation, and UI
  interpreters, plus the packages built on it: reflection-free compiled JSON codecs (`Axial.Schema.Json`), JSON
  Schema document generation (`Axial.Schema.JsonSchema`), host-neutral HTTP boundary support with OpenAPI and
  RFC 9457 problem details (`Axial.Schema.Http`, with ASP.NET Core and GenHTTP hosting adapters), and compile-time
  wire schema generation from `[<DeriveSchema>]` records or `.contract` files via `schemagen` and the
  `Axial.Schema.Contracts.Build` MSBuild package, including versioned contract chains.
- **`Axial`** — the top-level umbrella package installing `Axial.ErrorHandling`, `Axial.Schema`, and `Axial.Flow`
  together. App templates built on this umbrella are planned as follow-up work, not part of this release.
- Standardized pre-1.0 release versioning so every public Axial package in the release train ships at the same
  version from `Directory.Build.props`.
- Refreshed package metadata, README content, examples, generated reference pages, and documentation site content
  across the full package family.

Looking ahead: the repository itself is expected to split into `Axial`, `Axial.Schema`, and `Axial.Flow` repos
post-release, with the current repo becoming home to the root docs site and reference apps. Not yet decided
whether reference docs stay centralized or move per sub-repo.

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
