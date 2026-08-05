# Repository And Package Split

Status: planned work. Companion to `docs-information-architecture.md`, which owns the documentation plan.

Everything below is work to do or current state. Superseded directions are not repeated.

## Decision Summary

**Two independent projects, split on the one seam that is real — description versus execution.**

1. **Axial** (the current repository) — constraints, values, schema, and data. Keeps the name; in practice
   it already means this. Packages: `Axial.Result`, `Axial.Parse`, `Axial.Constraint`, `Axial.Refined`,
   `Axial.Data`, `Axial.Schema` and its satellites.
2. **FsFlow** (new repository) — the effect system and its satellites. Restores the identity last published
   at 0.6, which is the only released identity; nothing has ever shipped as `Axial.Flow`.

There is **no third repository** and no shared documentation site. Each project has one repository, one
site, one release train. The shared thesis — Axial encodes invariants about values, boundaries, and models;
FsFlow encodes invariants about computation — is stated in prose on each site, not encoded in package IDs.

Nothing has been published under the Axial name, so package boundaries and identities are free to change
today and breaking after first publish. That window governs the sequencing below.

## Current State

Landed in the combined repository:

- `Axial.Result` holds only general Result composition and `result { }`, plus `Accumulate.fs` (accumulating
  `result.list` / `result.array`), `Result.traverse`/`sequence`, `tap`/`tapError`, and `BindReturn`.
- `Axial.Constraint` holds the reusable path-free constraint surface and returns the standard F# `Result`,
  with no dependency on `Axial.Result`.
- `Axial.Refined` depends only on `Axial.Constraint`.
- `Axial.Parse` is a separate leaf depending on nothing.
- `Axial.Schema` depends on Data, Constraint, Refined, and Parse directly, never on `Axial.Result`.
- `Axial.Flow` depends on no other Axial package.
- Both meta-packages are gone: `src/Axial.ErrorHandling/` and `src/Axial/`, deleted with their solution,
  pack, and docs-build entries. `Axial.ApiShape.Tests` asserts no meta-package remains in the graph.
- Per-package test projects, AOT probes, source inventory checks, and doc generator inputs track the
  focused package set.

- Axial and FsFlow have separate version properties. `Directory.Build.props` declares `AxialVersion`
  and `FsFlowVersion` — both 0.7.0 for now, which is convenience, not a rule — and selects between them
  with `IsFsFlowProject`, which is true for
  `Axial.Flow*` and for the two HTTP adapters that become `FsFlow.AspNetCore` / `FsFlow.GenHttp`.
  `scripts/pack.sh` takes `-v` for the Axial train and `-f` for the FsFlow train; it no longer has a
  single `-p:Version` override that would silently re-couple them.
- Package-consumer fixtures exist at `tests/package-consumers/`, run by
  `scripts/run-package-consumers.sh`: Result alone, Parse alone, Refined alone, Schema alone, and
  FsToolkit + Refined/Schema. They restore from `artifacts/package` through their own `nuget.config`
  with `<clear />`, deliberately do not inherit the repository `Directory.Build.props`, and evict the
  matching version from the global cache first, so they see the packages as an outside consumer does.
  They are not in `Axial.slnx` — they only build after a pack.

Open follow-ups, still pre-extraction:

- Repository topics and release-notes vocabulary not audited against the current package names.

## Why Split

The repository asks maintainers and coding agents to hold two unrelated vocabularies at once. Schema work
uses structured data, schema, constraint, diagnostic, refined value, parse, wire contract, codec, migration.
Flow work uses environment, effect, scope, layer, fiber, cancellation, service, host.

Separate repositories give smaller trees, one architectural plan each, shorter agent instructions,
product-specific examples and benchmarks, independent release timing, less chance of a dependency appearing
between the two cores, and clearer issue ownership.

The split is not meant to prevent integration. Integration happens through released package dependencies
and two explicit adapters.

## Terms

A **repository** is a source-control and release-workflow boundary; several packages may live in one.
A **package** is a separately referenced NuGet unit with its own public dependency graph.
A **product** is a top-level package and documentation identity presented to users.

## Package Shape Before First Publish

Free now, breaking later. Do these in the combined repository.

### Deferred: merging `Axial.Constraint` into `Axial.Refined`

**Not decided. Constraint stays a separate package for now.**

The case for merging: `Constraint` alone is clunky in practice — check, then map or render — and anyone
going that far will likely take Schema too, so `Constraint` + `Refined` may be the real standalone unit
(domain invariants with no boundary and no serialization involved).

The case against acting yet: it is a one-way door for the vocabulary, and the clunkiness is an API
ergonomics problem that merging does not fix. Better to improve the standalone `Constraint` path first and
see whether the premise survives.

Worth knowing either way: package boundaries do not control payload. Trimming and tree-shaking work on
reachability, not package identity. What blocks shaking is module-level data — `Catalogue.entries` is one
such table — and that is unaffected by which package the code ships in. So there is no payload argument
pushing in either direction.

Revisit before first publish, since it is free now and breaking afterwards.

### Fold `Axial.Schema.JsonSchema` into `Axial.Schema`

Done. 635 lines in one file, depending only on Schema, and already declared in `namespace Axial.Schema` —
so the fold was a file move plus package deletion, with no call-site changes. JSON Schema emission is part
of "declare once, derive everything" rather than an optional extra.

`Axial.Parse` stays as it is: zero dependencies, a crisp standalone story, a self-explanatory name.

### Resulting package list — eleven, in three tiers

| Tier | Package | Depends on | API surface |
| --- | --- | --- | --- |
| Core | `Axial.Result` | — | yes |
| Core | `Axial.Parse` | — | yes |
| Core | `Axial.Constraint` | — | yes |
| Core | `Axial.Refined` | Constraint | yes |
| Core | `Axial.Data` | — | yes |
| Core | `Axial.Schema` | Constraint, Data, Parse, Refined | yes |
| Extension | `Axial.Schema.Json` | Schema | yes |
| Extension | `Axial.Schema.Contracts` | Schema | yes |
| Extension | `Axial.Schema.Http` | Schema | yes |
| Extension | `Axial.Schema.Testing` | Schema | yes |
| Build tooling | `Axial.Schema.Contracts.Build` | Contracts | **none** |

Down from 26 packages to 11. `Axial.Schema.JsonSchema` folded into `Axial.Schema`; `Axial.Constraint`
stays separate pending the deferred decision above.

The tiers are not presentational convenience. Core packages are what a reader chooses between; extensions
are added when a specific need arises; `Axial.Schema.Contracts.Build` is `DevelopmentDependency=true`,
`IncludeBuildOutput=false`, and compiles nothing — it ships an MSBuild targets file and a generator, so it
has no API and must be excluded from the reference entirely.

```bash
dotnet add package Axial.Result
dotnet add package Axial.Parse
dotnet add package Axial.Constraint
dotnet add package Axial.Refined
dotnet add package Axial.Data
dotnet add package Axial.Schema
```

### Namespace convention — unified on A

Every package now declares its own namespace equal to its package id, and `namespace Axial` is empty.

`Axial.Data` was the last holdout. It declared `namespace Axial` with `module Data` inside, so the module
path already equalled the package id — deliberate, but it meant `open Axial` was the way to reach Data, and
`[<AutoOpen>] module DataErgonomicsHelpers` leaked from the root namespace on any such open.

What made the move work, after one failed attempt:

- `namespace Axial.Data`, keeping `module Data`. The resulting `Axial.Data.Data` stutter is invisible at
  call sites — consumers write `open Axial.Data` then `Data.assoc` — and matches `Axial.Result.Result`.
- **`module Syntax` promoted out of `module Data` to namespace level**, so it stays `Axial.Data.Syntax` and
  every `open Axial.Data.Syntax` is unchanged. Its body needed `Data.` qualification on eight helpers it
  had been reaching unqualified from inside the enclosing module.
- **`module Json` deliberately left nested**, as `Axial.Data.Data.Json`. Promoting it would put a `Json`
  module in `Axial.Data` that shadows `Axial.Schema.Json`'s `module rec Json` in the twelve files that open
  both, and `Data.Json.render` is used directly in tests and examples. It is referenced as `Data.Json`, so
  nesting costs nothing.

Consumer impact was one line each: `open Axial` → `open Axial.Data`. Ten fully-qualified `Axial.Data.X`
references became `Axial.Data.Data.X`; the CLR module types moved from `Axial.DataModule` to
`Axial.Data.DataModule`, which two ApiShape assertions name directly.

Also fixed: `tests/Axial.Schema.Tests/SchemaTestSupport.fs` declared `namespace Axial`, squatting the root
namespace from a test project. Moved to `Axial.Tests`.

**Rule going forward: nothing declares into `namespace Axial`.** It is now empty, and keeping it that way
is what stops `open Axial` becoming an unscoped catch-all that drags every `[<AutoOpen>]` module with it.

Required regardless: the reference must state each type's package, since namespace alone cannot imply it
for satellites. See `docs-information-architecture.md` §6 item 5.

### Reserve NuGet prefixes

`Axial.*` and `FsFlow*`. Two applications; one per project.

The `Flow` package ID is held by an unlisted, .NET Standard 1.1 package with an empty repository. Requesting
a transfer costs little, but do not wait on it or plan around it — package ID and namespace are independent,
and the namespace stays qualified regardless, so winning it changes one line in a `.fsproj` and nothing a
user types.

## The HTTP Adapters Move To FsFlow

`Axial.Schema.Http.AspNetCore` → **`FsFlow.AspNetCore`**, `Axial.Schema.Http.GenHttp` → **`FsFlow.GenHttp`**.

These are the only two packages that depend on both products, so they determine how expensive the split is.
The code is unambiguous about where they belong:

- `Axial.Schema.Http` — the boundary abstraction itself, 406 lines across `BoundaryInput.fs`, `Endpoint.fs`,
  and `ProblemDetails.fs` — has **no Flow dependency at all**. It describes endpoints and emits an OpenAPI
  document via `OpenApi.document : OpenApiInfo -> EndpointSpec list -> string`. Contract-first use needs no
  server.
- The Flow coupling lives entirely in the two host adapters, and there it is structural rather than
  incidental. Flow is in the public API, not an implementation detail:

  ```fsharp
  let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model>
  (workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IResult>)
  ```

  An endpoint *is* a Flow with a request environment.

So they are Flow integrations that speak Schema, not Schema packages that happen to host. The dependency
direction inverts:

```text
released Axial.Schema.Http / Axial.Schema.Json / Axial.Data
            ↓
FsFlow.AspNetCore / FsFlow.GenHttp        (in the FsFlow repository)
```

This points the cross-repository version range at the **stable** side instead of at Flow's moving
environment surface, and leaves Axial with **zero** cross-product edges — it builds, tests, packs, and
releases with no FsFlow package present.

`Axial.Schema.Http` stays in Axial. It is already Flow-free and describes boundaries for any host.

**Naming.** `FsFlow.AspNetCore` and `FsFlow.GenHttp` sit at top level as siblings of `FsFlow.HttpClient` —
the client-side counterpart — rather than under `FsFlow.Hosting.*`, which is about application lifecycle
and platform host. Worth a second look during implementation; both readings are defensible.

**What the move touches:**

- `src/Axial.Schema.Http.{AspNetCore,GenHttp}/` — project rename, `PackageId`, `RootNamespace`,
  `AssemblyName`, and the module namespaces in `AspNetCore.fs` / `GenHttp.fs`.
- `tests/Axial.Schema.Http.Tests/` — holds `AspNetCoreAdapterTests.fs` and `GenHttpAdapterTests.fs`
  alongside the Schema.Http tests. Split; adapter tests go to FsFlow.
- `docs/schema/http-servers.md` and the generated reference trees under
  `docs/schema/reference/schema/http/{aspnetcore,genhttp}/` (~35 pages) move to FsFlow's documentation.
- `examples/Axial.Api` and `examples/Axial.Api.GenHttp` move with the packages.
- `scripts/pack.sh`, `scripts/docs-build.proj`, `scripts/docgen/Program.fs` symbol ids, and `Axial.slnx`.

Do the rename **before** extraction, while it is a single-repository refactor with the compiler checking
every call site.

## Repository Extraction

### Method

Use `git filter-repo` — not `filter-branch`, which is deprecated, and not a fork, which carries every
unrelated file's history forever and is marked as a fork by GitHub. Not currently installed:
`pip install git-filter-repo`.

**Asymmetric, deliberately:**

- **FsFlow** — fresh `git clone --no-local` into scratch, filter down to the Flow paths, push to a new
  empty repository. Clean history, `git blame` and `git bisect` intact across the 33 commits that touch
  `src/Axial.Flow`.
- **Axial** — an ordinary `git rm` commit removing the Flow paths. **Do not `filter-repo` the existing
  repository**: it rewrites every SHA and breaks existing clones and branches. Axial's history genuinely
  includes Flow and should keep saying so.

### Paths to extract

```
src/Axial.Flow                      src/Axial.Flow.PlatformService
src/Axial.Flow.Console              src/Axial.Flow.Process
src/Axial.Flow.FileSystem           src/Axial.Flow.Telemetry
src/Axial.Flow.Hosting              src/Axial.Flow.Telemetry.JavaScript
src/Axial.Flow.Hosting.Browser      src/Axial.Flow.Telemetry.Shared
src/Axial.Flow.Hosting.Node
src/Axial.Flow.HttpClient           src/Axial.Schema.Http.AspNetCore  ← renamed first
                                    src/Axial.Schema.Http.GenHttp

tests/Axial.Flow.Tests              tests/Axial.Flow.Integration.Tests
tests/Axial.Flow.Comparisons.Tests  tests/Axial.Flow.PlatformService.Tests
tests/Axial.Flow.FileSystem.Tests   tests/Axial.Flow.Telemetry.Tests
tests/Axial.Flow.Hosting.Tests
tests/Axial.Flow.HttpClient.Tests

docs/flow
benchmarks/Axial.Flow.Benchmarks
```

### The path list above is incomplete — it truncates history at 2026-06-21

**`filter-repo` matches path strings per commit and does not follow renames.** The Flow tree has been
renamed four times, so the `Axial.Flow*` names above only exist in the most recent era. Verified:

| Era | From | Paths |
| --- | --- | --- |
| 1 | 2026-03-30 | `src/EffectFs`, `tests/EffectFs.Tests`, `examples/EffectFs.*` |
| 2 | — | `src/EffectfulFlow`, `tests/EffectfulFlow.Tests`, `examples/EffectfulFlow.*` |
| 3 | — | `src/FlowKit`, `tests/FlowKit.Tests`, `examples/FlowKit.*` |
| 4 | 2026-04-27 | `src/FsFlow`, `src/FsFlow.{Capabilities,Caps,Services}.*`, `src/FsFlow.Hosting`, `src/FsFlow.Net`, `src/FsFlow.Runtime.Telemetry`, `tests/FsFlow.Tests`, `examples/FsFlow.*`, `benchmarks/FsFlow.Benchmarks{,.Fable}` |
| 5 | 2026-06-21 (`3a2a13f2`, "Split FsFlow into Axial packages") | `src/Axial.Flow*` — the list above |

`git log -- src/Axial.Flow` bottoms out at `3a2a13f2`. Filtering on the documented list alone would
produce an FsFlow repository whose history begins six weeks before the split and **omits the v0.6.0
tag entirely** — `git ls-tree v0.6.0 src/` is all `src/FsFlow*`, and 0.6 is the only version ever
published. That is the one loss in this whole plan that cannot be recovered afterwards.

Use `--path-glob` for the historical names, and check `git log --oneline | wc -l` on the result against
the ~231 commits that touch Flow lineage before pushing anywhere.

**Needs individual inspection before assignment — resolved.** Every example, benchmark, and cross-cutting
test project classified by its actual project references:

| Destination | Projects |
| --- | --- |
| **FsFlow** — Flow-only | `Axial.App.Example`, `Axial.Flow.AotProbe`, `Axial.Flow.Comparisons`, `Axial.Flow.PlatformService.Examples`, `Axial.Hosting.Browser`, `Axial.Hosting.Desktop`, `Axial.Hosting.GenericHost`, `Axial.Hosting.Node`, `benchmarks/Axial.Flow.Benchmarks` |
| **FsFlow** — adapter examples | `Axial.Api`, `Axial.Api.GenHttp` (no direct Flow reference, but they consume the AspNetCore/GenHttp adapters, which move) |
| **FsFlow** — with released Axial deps | `Axial.Hosting.DotNet` (Flow.Hosting plus incidental Parse and Refined) |
| **Axial** | `Axial.Constraint.AotProbe`, `Axial.Refined.AotProbe`, `Axial.Result.AotProbe`, `Axial.Schema.AotProbe`, `Axial.ReferenceApp.Intro`, `Axial.ReferenceApp.Wire`, `benchmarks/Axial.Schema.Benchmarks` |
| **Must split in two** | `Axial.Examples`, `Axial.MaintenanceExamples`, `Axial.Playground`, `Axial.ReadmeExample`, `benchmarks/Axial.Benchmarks.Fable`, `tests/Axial.ApiShape.Tests` |
| **Integration app** | `Axial.ReferenceApp` — consumes both sides plus both adapters. Becomes the integration application against released packages, per "Examples And Reference Applications". |

The six in "must split" each hold Flow content and Axial content in one project. `Axial.ApiShape.Tests` is
the most consequential: it asserts package layout across both products, so each repository needs its own
copy asserting only its own packages.

**A seventh: `tests/Axial.Flow.Tests`.** Found by the phase 6 verification — see "On phase 6" below. Its
runnable-example-docs test asserted both products' docs pages. Already split; listed here so the count
is right.

### Order

Keep history extraction separate from semantic refactoring. Do not combine filtering, renaming, API changes,
and optimization into one opaque migration.

1. Establish a final commit in the combined repository.
2. Create FsFlow from filtered history; verify tags, authors, and retained files.
3. Confirm the extracted repository builds and its tests pass standalone.
4. Make path and package renames in normal commits — `Axial.Flow` → `FsFlow`, and the two adapters.
5. Update build, CI, docs, and release configuration; install maintainer files.
6. Publish prerelease packages from each repository.
7. Run consumer and integration tests.
8. Remove the Flow paths from Axial only after FsFlow is usable.

## Versioning After The Split

Each repository has an independent release train. Packages within a repository may share one version while
coordinated releases remain convenient. Do not require the two repositories' versions to match — a tag such
as `v0.8.0` in Axial says nothing about FsFlow. FsFlow continues its own line from the published 0.6.

The FsFlow HTTP adapters should float within Axial's current pre-1.0 minor — `0.7.*` on
`Axial.Schema.Http`, `Axial.Schema.Json`, and `Axial.Data` — rather than pinning a closed range. While those
shapes are still moving, a float surfaces breaks in the adapter's own CI instead of at release time.

Two mechanical consequences to handle rather than inherit:

- NuGet resolves a float at **pack** time and writes it into the nuspec as an open-ended minimum, so
  consumers would see `>= 0.7.3` with no upper bound. Set an explicit upper bound in the packed metadata;
  the float is for the adapter's build, not for downstream constraints.
- Floating restores are not reproducible. Enable `RestorePackagesWithLockFile` so CI pins what it built.

Release notes describe only the packages in that repository.

## Testing Across Repositories

Each repository owns complete tests for its own public behavior.

Axial must test Result, Parse, Refined, Data, Schema, formats, and contract tooling — with no FsFlow package
anywhere in the tree.

FsFlow must test Flow, its services and hosts, and the two HTTP adapters. Only the adapter tests may
reference Axial, and only as released packages.

Add package-consumer tests that pack local artifacts and restore them into small fixture projects. This
catches missing package files, incorrect dependency ranges, build-target failures, and source-order problems.

Cross-product CI lives in FsFlow and should cover the current released Axial packages against the adapters,
the lowest Axial version the adapters claim to support, and the integration reference application against
released packages. Do not make either core repository's ordinary pull-request build depend on the other
repository's main branch.

## Examples And Reference Applications

Move product-specific examples with their repository. Axial examples cover input sources, private domain
construction, diagnostics, JSON, contracts, migrations, HTTP contract declaration, and property testing.
FsFlow examples cover dependencies, errors, cancellation, scopes, layers, concurrency, services, and hosts.

The combined reference application becomes either a separate integration repository consuming published
packages, or a small integration application in one repository consuming the other's published packages. A
separate repository gives the cleanest consumer test. It must not become a source dependency of either
product, and must not require unreleased source from both for routine builds.

## Repository-Specific Maintainer Files

Each repository needs its own `AGENTS.md`, `dev-docs/AGENT_INDEX.md`, `PLAN.md`, `TASKS.md`, `DOCS.md`,
`decisions/README.md`, `README.md`, release notes, source inventory, CI workflows, and package and
documentation scripts.

Remove instructions for the other product; do not copy the whole plan into both. Axial's agent index should
explain generated contract paths and documentation generation. FsFlow's should explain runtime, service
packages, hosts, platform targets, and effect-boundary rules.

## Release And CI

Each repository owns its version source, packing and signing, release tags and notes, source-link repository
URLs, NuGet publishing, documentation deployment, API compatibility checks, and the NativeAOT, trimming, and
Fable checks relevant to its packages.

Update repository URLs and source-link metadata before publishing from the new location.

## Implementation Sequence

| Phase | Work | Status |
| --- | --- | --- |
| 1 | Fold `Schema.JsonSchema` into `Schema` | **done** — 548a7b84 |
| 2 | Unify the namespace convention on A; empty `namespace Axial` | **done** — f3ab2d46, then completed here |
| 3 | Resolve the extraction path list and every ambiguous project | **done** — f3ab2d46 |
| 4 | Stop committing generated reference and `site/content` | **done** — 8d574579 |
| 5 | Split the version property in two; create package-consumer fixtures | **done** |
| 6 | Verify Flow builds and tests with no Axial source present | **done** — with one caveat below |
| 7 | `filter-repo` into FsFlow; install maintainer files and CI; confirm green | blocked — needs `git-filter-repo` installed and the target repository created |
| 8 | Rename `Axial.Flow` → `FsFlow`, including the two HTTP adapters | not started — see below |
| 9 | Publish prerelease FsFlow packages; run adapters against released Axial packages | not started |
| 10 | Remove Flow paths from Axial | not started |
| 11 | Documentation work — see `docs-information-architecture.md` | in progress |
| 12 | Prefix reservations; publish Axial, then FsFlow | not started |

Phases 1–4 were much cheaper in the combined repository, with the compiler checking every call site.

**On phase 6.** Verified by copying only `src/Axial.Flow*`, `tests/Axial.Flow*`,
`examples/Axial.Flow.Comparisons`, and `benchmarks/Axial.Flow.Benchmarks` into a scratch tree with no
Axial source and building it standalone: **build succeeded, 258 of 259 tests passed.** No Flow project
holds a `ProjectReference` to a non-Flow Axial project, and no Flow source file opens `Axial.Result`,
`Axial.Parse`, `Axial.Constraint`, `Axial.Refined`, `Axial.Data`, or `Axial.Schema`. The core seam is
genuinely clean.

The single failure was `Axial.Flow.Tests` asserting that **both** `docs/schema/examples.md` and
`docs/flow/examples.md` regenerate from `scripts/generate-example-docs.sh` — a cross-product test in a
Flow project, and the only coverage the schema page had. Now split: the Flow test passes `flow` and
asserts only the flow page, and `tests/Axial.Schema.Tests/ExampleDocsTests.fs` passes `schema` and
asserts only the schema page. `runBashScript` grew a `runBashScriptWithArguments` form to carry the
product argument. So **`tests/Axial.Flow.Tests` belongs on the "must split in two" list**, which it was
not on.

**Caveat, and a prerequisite that was not previously recorded.** The flow half of that generator renders
its page from `examples/Axial.Examples`, `examples/Axial.Playground`, and
`examples/Axial.MaintenanceExamples` — all three on the "must split in two" list, and between them they
reference `Axial.Result`, `Axial.Constraint`, `Axial.Refined`, `Axial.Parse`, `Axial.Schema`, and
`Axial.Data`. So FsFlow's docs test cannot go green until those three examples are split or repointed at
released Axial packages. Splitting the examples is a **prerequisite for FsFlow CI being green**, not
post-extraction tidying.

**On phase 8.** The rename is deliberately *after* extraction. In the combined repository it touches 1,011
occurrences across 212 files — down from 2,330 across 1,416 before phase 4 removed the generated trees —
and it would leave `FsFlow.*` packages sitting in the Axial repository alongside `docs/flow`, the Flow
sidebar, and `validate-flow-docs.sh`, all still named Axial. In the extracted repository it is the same
change against a tree that contains nothing else, with no confusing intermediate state. Nothing is blocked
by deferring it: phases 5 and 6 do not depend on the name.

## Acceptance Criteria

- Axial builds, tests, packs, and releases with no FsFlow package anywhere in the tree.
- FsFlow's core builds, tests, packs, and releases without checking out Axial.
- `FsFlow.AspNetCore` and `FsFlow.GenHttp` consume released Axial packages and are the only packages in
  either repository depending on both products.
- `Axial.Result`, `Axial.Parse`, `Axial.Refined`, and `Axial.Data` are independently installable.
- `Axial.Schema` depends on Data, Refined, and Parse directly; FsFlow's core depends on none of them.
- A clean consumer can install and run each product from published packages.
- The integration reference application works against published versions.
- Repository instructions contain no stale paths or rules from the other product.
- Release tags and notes no longer imply synchronized versions.

## Risks And Mitigations

**Cross-repository changes become slower.** Keep the core seam small, use package dependency ranges, test
adapters against released versions.

**The adapters lag behind Axial.** Float `0.7.*` so FsFlow's CI builds them against current Axial on every
run, and treat an adapter break as a signal about the Axial surface rather than only as adapter maintenance.

**History extraction obscures changes.** Extract first; make semantic changes in later ordinary commits.

**FsLiveDocs is on the critical path for documentation.** It is not on the critical path for the split.
Phases 1–9 need nothing from it.

## Choices To Resolve During Implementation

- Final GitHub repository name and documentation URL for FsFlow.
- Whether the integration reference application gets its own repository.
- Whether packages within a repository continue sharing a version after 1.0.
- The minimum Axial version supported by the FsFlow HTTP adapters.
- Whether the adapters sit at `FsFlow.*` or under `FsFlow.Hosting.*`.
- Where `benchmarks/Axial.Benchmarks.Fable` lives.

None requires returning to one combined repository.
