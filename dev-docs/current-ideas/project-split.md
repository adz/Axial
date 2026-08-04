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

Open follow-ups, still pre-extraction:

- No minimal package-consumer fixture projects (Result alone, Parse alone, Refined alone, Schema with its
  own deps, FsToolkit + Refined/Schema with no builder ambiguity). Coverage today is indirect.
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

### Namespace convention — settled

`Axial.Data` is now the only package using convention B, and it stays that way.

- **A** — namespace is the package id, module is the leaf: `Axial.Result` / `module Result`. You `open` the
  package id. Used by every package except Data.
- **B** — namespace is the parent, so the fully-qualified module path *equals* the package id: `Axial` /
  `module Data` → `Axial.Data`. You `open Axial`.

Converting Data to A was attempted twice and rejected both times:

1. `namespace Axial.Data` plus `module Data` yields `Axial.Data.Data`, and the nested modules become
   `Axial.Data.Data.Syntax` and `Axial.Data.Data.Json` — user-facing, since consumers write
   `open Axial.Data.Syntax` today. The build fails across `Axial.Data.Tests` and the generated contracts.
2. Promoting the nested modules to namespace level fixes the stutter but puts a `Json` module in
   `Axial.Data` that collides with `Axial.Schema.Json`'s `module rec Json` whenever both are opened. And
   `Data.Json.render` is used directly in tests and doc examples.

B is well-formed here: `module Data` carries `[<RequireQualifiedAccess>]` and is the package's entire API
surface, so nesting `Syntax` and `Json` beneath it is correct design rather than an accident.

The one real cost is that `[<AutoOpen>] module DataErgonomicsHelpers` sits in namespace `Axial`, so a bare
`open Axial` auto-opens it. That is harmless while Data is the only package declaring into the root
namespace — which it now is, and must remain. **No other package may declare into `namespace Axial`**;
doing so would put every AutoOpen module in one namespace and make `open Axial` unscoped.

Required regardless of convention: the reference must state each type's package, since namespace cannot
imply it. See `docs-information-architecture.md` §6 item 5.

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

| Phase | Work |
| --- | --- |
| 1 | Fold `Schema.JsonSchema` into `Schema`; move `Data` to convention A |
| 2 | Rename and move the HTTP adapters to `FsFlow.AspNetCore` / `FsFlow.GenHttp`; set the `0.7.*` floats and packed upper bound |
| 3 | Verify the extraction path list; resolve the ambiguous examples, benchmark, and ApiShape tests |
| 4 | Split the version property in two; create package-consumer fixtures for Flow and its satellites |
| 5 | Verify Flow builds and tests with no Axial source present |
| 6 | `filter-repo` into FsFlow; install maintainer files and CI; confirm green |
| 7 | Rename `Axial.Flow` → `FsFlow` in ordinary commits |
| 8 | Publish prerelease FsFlow packages; run adapters against released Axial packages |
| 9 | Remove Flow paths from Axial |
| 10 | Documentation work — see `docs-information-architecture.md` |
| 11 | Prefix reservations; publish Axial, then FsFlow |

Phases 1–3 are much cheaper in the combined repository, with the compiler checking every call site.

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
