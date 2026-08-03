# Repository, Package, And Documentation Split

Status: proposed repository direction. The package reorganization that must happen before extraction is largely done
in the combined repository (see "Completed So Far"), and Phase 1B(a) — the meta-package retirement — has now landed.
The HTTP adapter move and the docs reshape have not. No extraction has been performed.

This proposal separates Axial into products that can be understood, released, and used independently. Format
packaging and the .NET/Fable JSON runtime moved to `format-and-json-runtime.md`; neither blocks the split.

## Decision Summary

**Two product repositories, split on the one seam that is real — description-and-admission versus effectful
execution — plus the current repository retained as the documentation site.**

1. **Axial.Schema** (new) — Schema plus the values it admits and the data it reads. Carries `Axial.Data`, the Values
   packages (`Axial.Constraint`, `Axial.Refined`, `Axial.Parse`), `Axial.Result`, and every Schema satellite
   package.
2. **Axial.Flow** (new) — Flow and its satellite packages: platform services, hosts, telemetry, transports, and the
   relocated HTTP adapters.
3. **Axial** (the current repository) — the documentation site, the landing page, cross-product decisions, and the
   roadmap. No packages. Reference content is generated in the product repositories and assembled here.

Keeping `Axial` as the docs repository rather than renaming it to a product avoids privileging either one, and
keeps the umbrella name on the thing that is genuinely umbrella-shaped.

The products presented to users are **Result | Values | Data | Schema | Flow**. Values is a navigation grouping over
`Axial.Constraint`, `Axial.Refined`, and `Axial.Parse` — no package, no namespace. `Axial.Result` is a peer product,
not part of a bundle: since the accumulation work it carries `result.list`/`result.array`,
`Result.traverse`/`sequence`, and `tap`/`tapError`. `Axial.ErrorHandling` is deleted per
[retire-errorhandling.md](retire-errorhandling.md); this proposal assumes that outcome rather than re-arguing it.

### Why two, and why now

The shapes in group 1 are still moving — Constraint gained contextual localization, Parse and Refined are still
being separated (`refined-parse-cleanup.md`, `refined-schema-proof.md`), Result just grew accumulation. Splitting
them further now would freeze boundaries still being discovered, and buy nothing: they release together, are edited
together, and share reviewers.

Flow shares none of that. It has no dependency in either direction on Data, Schema, Result, Constraint, Refined, or
Parse, and its vocabulary does not overlap with theirs. That is the seam worth cutting.

A later third repository for Result alone stays possible once its shape settles. Do not attempt it in the same
operation as the Flow extraction.

### Package boundaries the split must not blur

Repository placement is not package placement. Within the Schema repository:

- `Axial.Result`, `Axial.Constraint`, `Axial.Parse`, and `Axial.Data` are independent leaves.
- `Axial.Refined` depends only on `Axial.Constraint`.
- `Axial.Schema` depends on Data, Constraint, Refined, and Parse — never on `Axial.Result`.

Install any leaf on its own; none installs Schema. Path-aware accumulation belongs to Schema; the flat accumulating
builders in `Axial.Result` are not a substitute for it.

### Search vocabulary

Retiring a package or navigation category must not retire its search vocabulary. NuGet tags and descriptions,
repository topics, page titles, comparison pages, and `llms.txt` keep using "error handling" and "validation" where
they name a user problem. Result keeps `result` and `error-handling`; Constraint keeps `validation`, `constraint`,
`predicate`, `result`, and `error-handling`; Schema keeps `validation`, `diagnostics`, and `schema`.

## Completed So Far (in the combined repository)

Ahead of any repository extraction:

- Documentation split into independent Schema and Flow experiences, then into focused per-package presentations;
  `Axial.Data` was promoted to its own product entry point (`ce58acad`), the precedent Result follows.
- `Axial.Result` holds only general Result composition and `result { }`, under the `Axial.Result` namespace, and
  has since been expanded with `Accumulate.fs` (accumulating `result.list` / `result.array` builders),
  `Result.traverse`/`sequence` (replacing the retired `Collection` module), `tap`/`tapError`, and `BindReturn`.
- `Axial.Constraint` holds the reusable path-free constraint surface and returns the standard F# `Result`. It has
  no dependency on `Axial.Result`.
- `Axial.Refined` depends only on `Axial.Constraint`; its own source uses plain FSharp.Core `Result.bind`/`map`/
  `mapError`, so no genuine Axial.Result dependency remains.
- `Axial.Parse` is a separate leaf with no dependency on Constraint, Refined, or Result.
- `Axial.Schema` depends on Data, Constraint, Refined, and Parse directly, never on `Axial.Result`.
- Per-package test projects, AOT probes, source inventory checks, and doc generator inputs track the focused package
  set; `scripts/docgen/Program.fs`, `scripts/generate-api-docs.sh`, `scripts/check-source-inventory.sh`, and
  `scripts/check-fable-js-surface.sh` all pass against the current source tree.
- Reference docs regenerate from XML comments and validate with `scripts/validate-docs.sh` and `site`'s
  `npm run build`.

### Not Done Yet (open follow-up, still pre-extraction)

- No full symmetric pass presenting every product the same way across every package README.
- No minimal package-consumer fixture projects (Result alone, Constraint alone, Parse alone, Refined+Constraint,
  Schema with its own deps, FsToolkit + Constraint/Refined/Schema with no builder ambiguity). Coverage today is
  indirect, via project references and `Axial.ApiShape.Tests`' package-layout assertions.
- Comparison pages (especially FsToolkit.ErrorHandling — now sharper given Result's accumulation surface),
  repository topics, and release-notes vocabulary not audited against the current package names.

These open items, plus the phases below, are the remaining pre-extraction and extraction work.

## Why Split The Repositories

The current repository asks maintainers and coding agents to keep two unrelated vocabularies in working memory.

Schema work uses terms such as structured data, schema, constraint, diagnostic, refined value, parse, wire contract,
codec, and migration. Flow work uses environment, effect, scope, layer, fiber, cancellation, service, and host.

Combining these products makes searches noisier and lengthens contributor instructions. It also makes a single release
version imply coordination that the package dependency graph does not require.

Separate repositories provide:

- smaller source and test trees;
- one architectural plan per product;
- shorter instructions for maintainers and coding agents;
- product-specific examples and benchmarks;
- independent release timing;
- less chance of adding a dependency between the two cores;
- clearer issue ownership and roadmaps;
- documentation that starts from one user problem.

The split is not intended to prevent integration. Integration should happen through ordinary released package
dependencies and a small number of explicit adapters.

## Terms Used In This Proposal

A **repository** is a source-control and release-workflow boundary. Several NuGet packages may live in one repository.

A **package** is a separately referenced NuGet unit with its own public dependency graph.

A **product** is a top-level package and documentation identity presented to users.

A **format package** implements one external representation, such as JSON or MessagePack, over Schema declarations.
See `format-and-json-runtime.md`.

## Target Repository: Axial Schema

The Schema repository owns the complete input-to-domain and domain-to-representation path, plus the value and
result vocabulary that path is expressed in.

Suggested repository name: `Axial.Schema`.

It should contain:

```text
Axial.Result
Axial.Constraint
Axial.Refined
Axial.Parse
Axial.Data
Axial.Schema
Axial.Schema.JsonSchema
Axial.Schema.Json
Axial.Schema.Testing
Axial.Schema.Contracts
Axial.Schema.Contracts.Build
Axial.Schema.Http
future Axial.Schema.<Format> packages
Schema, Values, and Result examples
benchmarks
documentation and site for Result, Values, Data, and Schema
contract generator and MSBuild integration
```

Both meta-packages are already absent: `Axial.ErrorHandling` and the `Axial` umbrella were deleted in Phase 1B(a).

### Why The Values Packages Stay Here

Schema depends on Constraint, Refined, and Parse, and their shapes are still being worked out against Schema's
needs (`refined-parse-cleanup.md`, `refined-schema-proof.md`). A repository boundary across that seam would freeze
it. They keep separate NuGet identities regardless.

### Why Result Stays Here

Nothing in this repository depends on `Axial.Result` — on dependency grounds it could be extracted today. It stays
because its surface only just changed, and because the story explaining flat accumulation in `result { }` versus
Schema's path-aware accumulation spans both products. Splitting first means writing that boundary twice, from two
sides. Revisit once the accumulating builders have been exercised by the reference app and `/result` has settled.

### Why Data Stays Here

Not because it is subordinate — `Axial.Data` is a product with its own docs entry point and its own journey. It
stays because it is the most heavily depended-on package in the repository: `Schema.parse` consumes `Data`
directly, and `Axial.Schema.Contracts`, `.Testing`, `.Http`, and `.Json` all reference it — as will the relocated
Flow HTTP adapters, through NuGet. Extracting it would put a package boundary across five edges to buy nothing.

### Schema Repository Dependency Graph

```text
Axial.Result                    (independent leaf, nothing here depends on it)
Axial.Parse                     (independent leaf)
Axial.Constraint ──▶ Axial.Refined

Axial.Data ────────┐
Axial.Constraint ──┤
Axial.Refined ─────┼──▶ Axial.Schema
Axial.Parse ───────┘        ├── Axial.Schema.Json          (+ Axial.Data)
                            ├── Axial.Schema.JsonSchema
                            ├── Axial.Schema.Testing       (+ Axial.Data)
                            ├── Axial.Schema.Http          (+ Axial.Data, JsonSchema)
                            └── generated contract output

Axial.Schema.Contracts ──▶ Axial.Data
        ↓ tool output targets Axial.Schema

Axial.Schema.Contracts.Build
        ↓ invokes the contract generator during MSBuild
```

The contracts generator remains a tool-tier component. FSharp.Compiler.Service must not become a dependency of a
runtime package.

## Target Repository: Axial Flow

The Flow repository owns workflow description, execution, operational services, resource handling, and hosting.

Suggested repository name: `Axial.Flow`.

It should contain:

```text
Axial.Flow
Axial.Flow.PlatformService
Axial.Flow.Console
Axial.Flow.FileSystem
Axial.Flow.HttpClient
Axial.Flow.Process
Axial.Flow.Hosting
Axial.Flow.Hosting.Node
Axial.Flow.Hosting.Browser
Axial.Flow.Telemetry
Axial.Flow.Telemetry.Shared
Axial.Flow.Telemetry.JavaScript
Axial.Flow.AspNetCore          (was Axial.Schema.Http.AspNetCore)
Axial.Flow.GenHttp             (was Axial.Schema.Http.GenHttp)
other Flow service and transport packages
Flow examples
Flow benchmarks
Flow documentation and site
```

The **core** `Axial.Flow` package must remain independent of `Axial.Result`, `Axial.Constraint`, `Axial.Refined`,
`Axial.Parse`, `Axial.Data`, and `Axial.Schema`. This constraint binds the core, not every package in the
repository: the HTTP adapters below are satellites that consume released Schema packages.

Flow binds the standard F# `Result<'value, 'error>` and `Option<'value>` types directly. It does not need the
Result package to support typed failures.

Flow policies may accept ordinary functions returning standard `Result`. They must not create a package dependency on
Schema merely to provide convenience adapters.

## The HTTP Adapters Move To Flow And Are Renamed

`Axial.Schema.Http.AspNetCore` → **`Axial.Flow.AspNetCore`**, `Axial.Schema.Http.GenHttp` → **`Axial.Flow.GenHttp`**,
both in the Flow repository.

These are the only two packages in the codebase that depend on both products, so they determine how expensive the
split is. The earlier plan kept them with Schema on the grounds that they adapt schema-described HTTP boundaries.
The code says otherwise:

- `Axial.Schema.Http` — the boundary abstraction itself, 406 lines across `BoundaryInput.fs`, `Endpoint.fs`, and
  `ProblemDetails.fs` — has **no Flow dependency at all**.
- The Flow coupling lives entirely in the two host adapters, and there it is structural, not incidental:
  `Flow.read`, `Flow.localEnv`, `Flow.fromTask`, a request-scoped `HttpEndpointEnv`, and `EndpointFlow.run`. An
  endpoint *is* a Flow with a request environment. The public names already say so.

So they are Flow integrations that speak Schema, not Schema packages that happen to host. They belong with the code
they change alongside: they consume Flow's environment APIs, and the environment/provide story is the part of Flow
still expected to move — exercising it is exactly what a satellite package is for.

The dependency direction inverts:

```text
released Axial.Schema.Http / Axial.Schema.Json / Axial.Data
            ↓
Axial.Flow.AspNetCore / Axial.Flow.GenHttp        (in the Flow repository)
```

This is strictly better than the previous arrangement. The cross-repository version range now points at the
**stable** side — Schema.Http, Schema.Json, and Data — instead of at Flow's moving environment surface. And the
Schema repository ends up with **zero** cross-product edges: it builds, tests, packs, and releases with no Flow
package present at all.

`Axial.Schema.Http` itself stays with Schema. It is already Flow-free and describes boundaries for any host.

These adapters should release only when their own code or dependency requirements change. A Schema release must not
automatically force a Flow repository release.

### Naming

`Axial.Flow.AspNetCore` and `Axial.Flow.GenHttp` sit at the top level, as siblings of `Axial.Flow.HttpClient` — the
client-side counterpart — rather than under `Axial.Flow.Hosting.*` alongside `Hosting.Node` and `Hosting.Browser`.
`Hosting.*` is about application lifecycle and platform host; these serve HTTP endpoints. Worth a second look during
implementation, since both readings are defensible.

### What The Move Touches

- `src/Axial.Schema.Http.{AspNetCore,GenHttp}/` — project rename, `PackageId`, `RootNamespace`, `AssemblyName`, and
  the module namespaces in `AspNetCore.fs` / `GenHttp.fs`.
- `tests/Axial.Schema.Http.Tests/` — currently holds `AspNetCoreAdapterTests.fs` and `GenHttpAdapterTests.fs`
  alongside the Schema.Http tests. Split: adapter tests go to the Flow repository.
- `docs/schema/http-servers.md` and the two generated reference trees under
  `docs/schema/reference/schema/http/{aspnetcore,genhttp}/` (~35 pages) move to the Flow documentation, with
  redirects. `site/data/sidebars/schema.yaml` loses those groups; the Flow sidebar gains them.
- `examples/Axial.Api` and `examples/Axial.Api.GenHttp` are adapter examples and move with the packages.
  `examples/Axial.ReferenceApp` stays cross-product and consumes released packages either way.
- `scripts/pack.sh`, `scripts/docs-build.proj`, `scripts/docgen/Program.fs` symbol ids, and `Axial.slnx`.

Do the rename **before** extraction, while it is a single-repository refactor with a compiler checking every call
site.

## No Meta-Packages

The `Axial` umbrella tied Schema and the leaves to one installation and apparent release train. It is gone, deleted
alongside `Axial.ErrorHandling` in Phase 1B(a).

`Axial.ErrorHandling` goes too, per `retire-errorhandling.md`. It bought a searchable category at the cost of a
documentation area, a sidebar group, a URL prefix over four unrelated packages, and a validation script — and the
bundle it named is not a concept adopters reason about: Result is about failure composition, Constraint/Refined/Parse
about admitting values. The category survives as **Values** in navigation and as package tags, with no package
behind it.

Every install is therefore a focused package:

```bash
dotnet add package Axial.Result
dotnet add package Axial.Constraint
dotnet add package Axial.Refined
dotnet add package Axial.Parse
dotnet add package Axial.Data
dotnet add package Axial.Schema
dotnet add package Axial.Flow
```

Nav captions and landing pages must state that the Values packages install independently, or adopters will search
NuGet for a nonexistent `Axial.Values`.

## Package Versioning After The Split

Each repository should have an independent release train.

Packages within the Schema repository may initially share one version if coordinated releases remain convenient. The
same applies within the Flow repository.

Do not require the Schema and Flow repository versions to match.

The Flow HTTP adapters should float within the current pre-1.0 minor — `0.7.*` on `Axial.Schema.Http`,
`Axial.Schema.Json`, and `Axial.Data` — rather than pinning a closed range. While those shapes are still moving, a
float surfaces breaks in the adapter's own CI instead of at release time.

Two mechanical consequences to handle rather than inherit:

- NuGet resolves a float at **pack** time and writes the resolved version into the nuspec as an open-ended minimum,
  so consumers would see `>= 0.7.3` with no upper bound. Set an explicit upper bound in the packed metadata; the
  float is for the adapter's build, not for downstream constraints.
- Floating restores are not reproducible. Enable `RestorePackagesWithLockFile` so CI pins what it actually built.

Release notes should describe only the product and packages in that repository. Avoid a global Axial release note that
mixes unrelated changes.

## Focused Documentation Libraries

The top-tier menu is **Result | Values | Data | Schema | Flow**, and is live. Values is a navigation group over
Constraint, Refined, and Parse, each with its own subgroup and API reference index; the other four are products
backed by packages. The page lists below are the target contents of each area — the areas themselves exist.

They deploy from the current site infrastructure today, as five peer areas. A reader entering one library should
encounter only the dependencies and related concepts needed for that path.

### Result Documentation

```text
Axial Result
  Overview
  Getting started
  Result composition
  Computation expression
  Accumulating errors (result.list / result.array)
  traverse and sequence
  Comparison with FsToolkit.ErrorHandling
  API reference
```

Titles and descriptions should naturally include "F# error handling" for discovery.

The accumulation page must show the exact `and!`-accumulates / `let!`-fails-fast boundary — "why didn't it collect
both errors" is the question adopters will ask — and state that this accumulation is flat, pointing to Schema for
path-aware diagnostics.

### Values Documentation

```text
Axial Values
  Overview (three independently installable packages)
  Constraint
    Getting started
    Reusable constraints
    Constraint composition
    Predicates
    Contextual localization
    API reference
  Refined
    Getting started
    Define refinements
    Built-in refined values
    Use constraints in refinements
    API reference
  Parse
    Getting started
    Parse representations
    API reference
```

The Constraint overview should say that constraints return the standard F# `Result` and work with Axial.Result,
FsToolkit.ErrorHandling, or application-owned helpers. Refined depends on Constraint but remains usable without
Result or Schema; Parse depends on neither. Every Values caption must state that the packages install individually.

### Schema Documentation

```text
Axial Schema
  Overview
  Getting started
  Parse structured input
  Construct domain models
  Field constraints
  Refined fields
  Path-aware errors
  JSON
  JSON Schema
  Wire contracts and migrations
  HTTP boundaries
  Recommended patterns
  Testing
  API reference
```

The JSON pages should use the `Axial.Schema.Json` name and distinguish trusted codec decoding from full
`Schema.parse` diagnostics.

Future format packages should receive their own section only after they exist.

### Flow Documentation

```text
Axial Flow
  Overview
  Getting started
  Write workflows
  Dependencies and environments
  Typed failures and defects
  Cancellation
  Resources and scopes
  Layers
  Concurrency
  Hosting
  Operational services
  Testing
  API reference
```

Flow examples should not introduce Schema as part of the basic path. Use ordinary inputs and standard F# `Result`; do
not imply that typed Flow failures require Axial.Result.

### Cross-Links

Keep cross-links small and specific:

- Schema field pages may link to Constraint for standalone reusable constraints and Refined for invariant-carrying
  values.
- Schema HTTP pages may say that handlers can return ordinary tasks or Axial Flow workflows.
- Flow pages may show a later example receiving a value admitted by Axial Schema.
- Each library home may link to the others under "Related Axial libraries."

The Values landing page routes readers to Constraint, Refined, or Parse without duplicating their guides, and links
to Result for composing what they return. The root landing page shows all five products while guiding newcomers
toward Result for simple code and Schema for structured boundaries.

## Documentation Deployment Options

The preferred final state is one documentation deployment per repository. Possible addresses include separate
subdomains or stable path prefixes.

Stable path prefixes fit the focused presentation without requiring separate deployments:

```text
axial.dev/result
axial.dev/values/{constraint,refined,parse}
axial.dev/data
axial.dev/schema
axial.dev/flow
```

Repository independence matters more than the URL shape. Each repository should be able to build and deploy its own
documentation without checking out the other.

If a shared landing site remains, keep it small. It should identify the focused libraries and link to their documentation.
It should not duplicate their guides or API references.

## Examples And Reference Applications

Move product-specific examples with their repository.

Schema examples should cover input sources, private domain construction, diagnostics, JSON, contracts, migrations,
HTTP boundaries, and property testing.

Flow examples should cover dependencies, errors, cancellation, scopes, layers, concurrency, services, and hosts.

The current combined reference application may become:

- a separate integration repository consuming published packages; or
- a small integration application in the Schema repository consuming published Flow packages.

A separate repository gives the cleanest consumer test. It must not become a source dependency of either product.

The integration application should test supported released combinations. It should not require unreleased source from
both repositories for routine builds.

## Testing Across Repositories

Each repository owns complete tests for its own public behavior.

The Schema repository must test Result, the Values packages, Data, Schema, formats, and contract tooling — with no
Flow package present anywhere in the tree.

The Flow repository must test Flow, its services and hosts, and the HTTP adapters. Only the adapter tests may
reference Schema, and only as released packages.

Add package-consumer tests that pack local artifacts and restore them into small fixture projects. This catches missing
package files, incorrect dependency ranges, build-target failures, and source-order problems.

Cross-product CI lives in the Flow repository and should include:

- the current released Schema packages against the adapters;
- the lowest Schema version the adapters claim to support;
- a scheduled check against the latest prerelease only if early warning is worth the maintenance cost;
- the integration reference application against released packages.

Do not make either core repository's ordinary pull-request build depend on the availability of the other repository's
main branch.

## Source And History Migration

Preserve useful Git history when creating the repositories. Use a history-filtering tool to retain relevant paths, then
perform package moves and renames in later commits.

Keep history extraction separate from semantic refactoring:

1. establish a final commit in the combined repository;
2. create each repository from filtered history;
3. verify tags, authors, and retained files;
4. make path and package renames in normal commits;
5. update build, CI, docs, and release configuration;
6. publish prerelease packages from each repository;
7. run consumer and integration tests;
8. archive or redirect the old repository only after both replacements are usable.

Do not combine history filtering, namespace renaming, API changes, and runtime optimization into one opaque migration.

## Repository-Specific Maintainer Files

Each new repository needs its own:

```text
AGENTS.md
dev-docs/AGENT_INDEX.md
dev-docs/PLAN.md
dev-docs/TASKS.md
dev-docs/DOCS.md
dev-docs/decisions/README.md
README.md
release notes
source inventory
CI workflows
package and documentation scripts
```

Remove instructions for the other product. Do not copy the entire current plan into both repositories.

The Schema agent index should explain generated contract paths and documentation generation. The Flow agent index
should explain runtime, service packages, hosts, platform targets, and effect-boundary rules.

## Release And CI Changes

Each repository should own:

- its package version source;
- package packing and signing;
- release tags;
- release notes;
- source-link repository URLs;
- NuGet publishing;
- documentation deployment;
- API compatibility checks;
- NativeAOT, trimming, and Fable checks relevant to its packages.

Update repository URLs and source-link metadata before publishing from the new location.

Tags should be repository-local. A tag such as `v0.8.0` in the Schema repository says nothing about the Flow version.

If packages within one repository later need independent versions, make that a separate decision. The repository split
does not require solving every package-versioning question at once.

## Implementation Sequence

### Phase 1: Package Surface

Landed in the combined repository: focused leaves, per-package tests and reference docs, Data promoted to its own
entry point, Result expanded with accumulation. See "Completed So Far".

Flow leaves early, because the noisiest remaining work — the documentation reshape — is much cheaper in a repository
that no longer contains it. What must precede the Flow extraction is only what actually blocks it.

### Phase 1B(a): Untangle The Meta-Packages

**Done**, except for the placement decision in step 3. Package half of `retire-errorhandling.md`, and a hard
precondition for extracting Flow.

Both meta-packages are deleted: `src/Axial.ErrorHandling/` and `src/Axial/` (whose only source was a one-line
re-export of `Axial.Result.Builders.result`), along with their solution, pack, and docs-build entries. The four
Values leaves are now listed explicitly in `scripts/build-docs-site.sh` and `scripts/docs-build.proj`, which
previously reached them through the umbrella.

Seven projects straddled the split, every one of them because of a meta-package. Six are now resolved by focused
references; `Axial.Benchmarks.Fable` never used a meta-package and straddles on genuine content:

| Project | Was | Now |
| --- | --- | --- |
| `examples/Axial.Hosting.DotNet` | `Axial.ErrorHandling` + `Axial.Flow.Hosting` | Refined + Parse + Flow.Hosting |
| `examples/Axial.{MaintenanceExamples,ReadmeExample,Playground}` | umbrella + Flow | Result + Constraint + Flow |
| `examples/Axial.Examples` | umbrella + Flow + Data | the four leaves + Schema + Flow + Data |
| `examples/Axial.ReferenceApp` | umbrella + Flow + Schema.* + Data | Result + Constraint + Refined + Schema + the rest unchanged |
| `examples/Axial.ReferenceApp.Intro` | `Axial.ErrorHandling` | the four leaves |
| `tests/Axial.ApiShape.Tests` | umbrella + Schema.* + five Flow packages | the four leaves + Schema + the rest unchanged |
| `benchmarks/Axial.Benchmarks.Fable` | Flow + Result + Constraint + Schema.Json | unchanged — genuinely cross-product |

`Axial.ApiShape.Tests` gained `no meta-package remains in the graph`, which fails if any package assembly references
`Axial` or `Axial.ErrorHandling`, or if either DLL reappears in the test output. It replaces the old assertion that
the meta-package existed and exported nothing.

The documentation split came with it rather than waiting for Phase 3, because deleting the meta-package while the
navigation still advertised it would have been incoherent. See "Phase 3" below.

Still open — step 3: where the reference application and the Fable benchmark live. Both are genuinely
cross-product, and neither blocks the HTTP adapter move in 1B(b). `examples/Axial.ReferenceApp` has a stated
direction already (see "Examples And Reference Applications" and the implementation-choices list);
`benchmarks/Axial.Benchmarks.Fable` does not, and is the one real decision left in this phase — it benchmarks Flow,
Result, Constraint, and Schema.Json under Fable in a single project, so it either splits along the product seam or
becomes a consumer of released packages.

### Phase 1B(b): Move And Rename The HTTP Adapters

Not started. See "The HTTP Adapters Move To Flow And Are Renamed" for the full change list. Do it here, as a
single-repository refactor with the compiler checking every call site, and set the `0.7.*` floats plus the packed
upper bound while both sides are still in one tree.

### Phase 2: Extract Flow

1. Split one version property into two; remove one-solution and one-release-note assumptions on the Flow side.
2. Create package-consumer fixtures for Flow and its satellites.
3. Verify Flow builds and tests with no Schema, Data, Result, or Values files present.
4. Filter history into the Flow repository; install its maintainer files and CI.
5. Publish prerelease Flow packages; run the adapters against released Schema packages.
6. Deploy the Flow documentation entry point.

### Phase 3: Reshape Documentation

Step 1 is **done**, pulled forward to land with 1B(a). Step 2 is not started.

1. **Done.** Docs half of `retire-errorhandling.md`. The top navigation is now **Result | Values | Data | Schema |
   Flow**, five peer areas, each with its own landing page, sidebar file, generated reference tree, `agent.md`, and
   `llms.txt`. `docs/error-handling/` is gone: Result took its guides, the FsToolkit comparison, and
   `reference/result/`; Values took Constraint, Refined, Parse, the tutorials, the introductory reference app, and
   their reference trees. The shared prose pages were split, not duplicated. `populate-hugo-content.sh` now
   iterates a product list instead of four hardcoded pairs, `AXIAL_DOCS_PRODUCT` takes `result`/`values` in place
   of `validation`, and `validate-error-handling-docs.sh` became `validate-result-docs.sh` +
   `validate-values-docs.sh`.

   Result is a **peer of Values, not a member of it** — they answer different questions, and nesting Result under
   Values reproduces exactly the meta-package framing this split removes.
2. Migrate reference generation to FsLiveDocs in place, while content and pipeline are still co-located.

### Phase 4: Extract Schema, Leaving Axial As The Documentation Repository

1. filter history into the Schema repository;
2. `site/` and the cross-product landing content stay behind in `Axial`;
3. transfer product-specific issues out of `Axial`; keep cross-product roadmap and decisions there;
4. install repository-specific maintainer files and CI;
5. verify source inventories and generated paths;
6. publish prerelease packages, then stable when consumer tests pass;
7. run the external reference application.

## Acceptance Criteria

The split is complete when:

- Schema builds, tests, packs, and releases with no Flow package anywhere in the tree;
- Flow's core builds, tests, packs, and releases without checking out Schema;
- `Axial.Flow.AspNetCore` and `Axial.Flow.GenHttp` consume released Schema packages, and are the only packages in
  either repository that depend on both products;
- `Axial.Result`, `Axial.Constraint`, `Axial.Refined`, `Axial.Parse`, and `Axial.Data` are independently
  installable (done);
- `Axial.Refined` depends only on Constraint (done);
- `Axial.Schema` depends on Data, Constraint, Refined, and Parse directly, while Flow depends on none of them (done);
- neither `Axial.ErrorHandling` nor the `Axial` umbrella remains (done);
- Result, Values, Data, Schema, and Flow have distinct documentation identities, each a top-nav entry with its own
  area, sidebar, reference tree, and `llms.txt` (done);
- NuGet, GitHub, and web searches for Result, F# error handling, validation, and diagnostics lead to the relevant
  packages or documentation;
- a clean consumer can install and run each product from published packages;
- the combined reference application works against published versions;
- repository instructions contain no stale paths or rules from the other product;
- release tags and notes no longer imply synchronized Schema and Flow versions.

## Risks And Mitigations

### Cross-Repository Changes Become Slower

Mitigation: keep the core seam small, use package dependency ranges, and test adapters against released versions.

### The Adapters Lag Behind Schema

Mitigation: float `0.7.*` so the Flow repository's CI builds them against current Schema on every run, and treat an
adapter break as a signal about the Schema surface rather than only as adapter maintenance.

### Documentation Drifts

Mitigation: each repository owns its docs and references. Keep cross-links sparse and check them during deployment.

### History Extraction Obscures Changes

Mitigation: extract history first and make semantic changes in later ordinary commits.

## Decisions This Proposal Makes

- Two product repositories: Schema and Flow. Result stays with Schema for now; a third repository is a later,
  separate decision.
- Result, Constraint, Refined, Parse, and Data remain focused package boundaries inside the Schema repository.
- No meta-packages: both `Axial.ErrorHandling` and the `Axial` umbrella are removed. Values is navigation only.
- The `Axial.Flow` core remains independent of Result, Constraint, Refined, Parse, Data, and Schema; the
  constraint binds the core, not every package in the Flow repository.
- The HTTP adapters move to the Flow repository as `Axial.Flow.AspNetCore` and `Axial.Flow.GenHttp`, consuming
  released Schema packages. `Axial.Schema.Http` stays with Schema.
- Flow is extracted first; the documentation reshape happens afterwards, in the smaller repository.
- The current repository ends as `Axial`, the documentation site and cross-product tracker.
- Documentation presents Result, Values, Data, Schema, and Flow before source repositories are extracted.

## Choices To Resolve During Implementation

These choices do not change the main direction:

- final GitHub repository names and documentation URLs;
- whether the combined reference application receives its own repository;
- whether Schema repository packages continue sharing a version after 1.0;
- whether Flow repository packages continue sharing a version after 1.0;
- the minimum Schema version supported by the Flow HTTP adapters;
- whether the adapters sit at `Axial.Flow.*` or under `Axial.Flow.Hosting.*`;
- whether `Axial.Result` eventually gets its own repository.

Resolve these with consumer examples and package tests. None requires returning to one combined repository.
