# Repository, Package, And Documentation Split

Status: proposed repository direction. The package/namespace/documentation reorganization that must happen before
extraction is mostly complete in the combined repository (see "Completed So Far"). The source/release repository
split itself (Phase 2 onward below) remains proposed and has not been started.

This proposal separates Axial into products that can be understood, released, and used independently. It also defines
how .NET and Fable implementations should share an API without sharing the wrong runtime assumptions.

## Decision Summary

Axial presents five focused libraries:

1. **Result** provides ordinary `Result` composition and `result { }`.
2. **Check** provides reusable path-free value constraints returning the standard F# `Result`.
3. **Refined** parses and constructs domain values whose invariants are represented by their types.
4. **Schema** describes structured boundaries, accumulated path-aware errors, codecs, and contracts.
5. **Flow** describes and runs effectful work with explicit dependencies, expected failures, cancellation, and
   resources.

These are package and documentation identities, not five equally weighted answers to "where do I start?" The primary
onboarding choice remains ordinary `Result` for simple operations or Schema for admitting structured data into domain
models. Introduce Check when reusable value constraints emerge, Refined when a value should carry proof of an
invariant, and Flow when the operation becomes effectful.

Result, Check, and Refined should eventually live in a repository separate from Flow. Their core packages already have
no dependency in either direction from Flow.

`Axial.Result` and `Axial.Check` are independent leaf packages — neither depends on the other. `Axial.Refined` depends
on Check, not on Axial.Result. `Axial.ErrorHandling` is a dependency-only meta-package installing Result, Check, and
Refined directly; it is the searchable category and convenient complete installation, not a namespace users open and
not a dependency of Schema.

Removing a package or navigation category must not remove its search vocabulary. NuGet descriptions and tags,
repository topics, page titles, descriptions, comparison pages, and `llms.txt` should continue to use the phrases
"error handling" and "validation" where they describe a user problem. Result should retain `result` and
`error-handling` tags; Check should retain `validation`, `check`, `predicate`, `result`, and `error-handling`; Schema
should retain `validation`, `diagnostics`, and `schema`. This preserves discovery without collapsing the focused
package boundaries.

Future formats should receive separate packages rather than being collected behind one generic codec package.

## Completed So Far (in the combined repository)

Landed 2026-07-21 through 2026-07-24, ahead of any repository extraction:

- Documentation split into independent Schema and Flow experiences, later superseded by the current focused
  Error Handling (Result/Check/Refined) / Schema / Flow presentation, with the top-tier menu staying
  `Error Handling | Schema | Flow`.
- `Axial.Result` holds only general Result composition and `result { }`, under the `Axial.Result` namespace.
- `Axial.Check` (new package) holds `Check<'value>`, `CheckFailure`, `Predicate`, and `CheckDSL`, under `Axial.Check`
  / `Axial.Check.CheckDSL`. It has no dependency on `Axial.Result`.
- `Axial.Refined` depends only on `Axial.Check`; its own source uses `Check.*` and plain FSharp.Core
  `Result.bind`/`map`/`mapError`, so no genuine Axial.Result dependency remained.
- `Axial.Schema` depends on `Axial.Check` and `Axial.Refined` directly, never on `Axial.Result` or
  `Axial.ErrorHandling`.
- `Axial.ErrorHandling` is a true dependency-only package (`IncludeBuildOutput=false`, no `.fs` files) that installs
  Result, Check, and Refined directly; the packed `.nupkg` was verified to carry no `lib/` assembly.
- Tests, AOT probes, examples, source inventory checks, and doc generator inputs were updated: `tests/Axial.Result.Tests`,
  `tests/Axial.Check.Tests`, `tests/Axial.Refined.Tests` replaced the combined `Axial.ErrorHandling.Tests`;
  `examples/Axial.Check.AotProbe` was split out; `scripts/docgen/Program.fs`, `scripts/generate-api-docs.sh`,
  `scripts/check-source-inventory.sh`, and `scripts/check-fable-js-surface.sh` all pass against the new source tree.
- Reference docs for Result/Check/Refined were regenerated from XML comments and validated with
  `scripts/validate-docs.sh` and `site`'s `npm run build`.
- Package tags were updated to the target search vocabulary (see below) for Result, Check, Refined, Schema, and
  ErrorHandling.

### Not Done Yet (open follow-up, still pre-extraction)

- The broad `Axial` umbrella package was **kept**, not removed. Several examples and `Axial.ApiShape.Tests` still use
  it for single-package convenience across Error Handling + Schema. Removing it requires rewriting those onto the
  narrowest focused package set.
- No distinct Check/Refined site-navigation sub-pages or new redirects were added beyond what the existing
  generator/content-mirror pipeline produces from the updated source tree.
- Root README and package READMEs name Result/Check/Refined with focused install commands, but a full symmetric pass
  presenting all five libraries the same way across every package README was not done.
- Six dedicated, minimal package-consumer fixture projects (Result alone, Check alone, Refined+Check without Result,
  ErrorHandling installs all three, Schema installs its own deps, FsToolkit + Check/Refined/Schema with no builder
  ambiguity) were not created. Coverage today is indirect, via each focused test project's own project references and
  `Axial.ApiShape.Tests`' package-layout assertions.
- Comparison pages (especially FsToolkit.ErrorHandling), repository topics, and release-notes vocabulary were not
  audited against the new package names.

These open items, plus Phases 2–4 below, are the remaining pre-extraction (and extraction) work.

## Why Split The Repositories

The current repository asks maintainers and coding agents to keep two unrelated vocabularies in working memory.

Schema work uses terms such as structured data, schema, constraint, diagnostic, refined value, wire contract, codec, and
migration. Flow work uses environment, effect, scope, layer, fiber, cancellation, service, and host.

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

A **library** is a top-level package and documentation identity presented to users.

A **format package** implements one external representation, such as JSON or MessagePack, over Schema declarations.

A **runtime backend** is the internal implementation chosen for a compilation platform. It does not imply a separate
public package.

## Target Repository: Axial Schema

The Schema repository owns the complete input-to-domain and domain-to-representation path.

Suggested repository name: `Axial.Schema`.

It should contain:

```text
Axial.ErrorHandling
Axial.Result
Axial.Check
Axial.Refined
Axial.Data
Axial.Schema
Axial.Schema.JsonSchema
Axial.Schema.Json
Axial.Schema.Testing
Axial.Schema.Contracts
Axial.Schema.Contracts.Build
Axial.Schema.Http
Axial.Schema.Http.AspNetCore
Axial.Schema.Http.GenHttp
future Axial.Schema.<Format> packages
Schema examples
Schema benchmarks
Schema documentation and site
contract generator and MSBuild integration
```

### Why Result, Check, And Refined Stay Here

`Axial.Result` and `Axial.Check` are independent leaf packages. Neither depends on Refined, Schema, or Flow.
`Axial.Refined` depends only on Check. `Axial.ErrorHandling` is a dependency-only meta-package over all three.

They belong in the Schema repository because Schema consumes Check and Refined, while Result serves the adjacent
ordinary-error-handling path. All three participate in turning untrusted inputs and fallible operations into
application values while retaining separate package identities.

Putting Result, Check, or Refined in additional repositories would add release and contribution boundaries without
improving the Schema/Flow separation.

The repository placement must not blur the NuGet boundary. Install `Axial.Result`, `Axial.Check`, or `Axial.Refined`
for focused use, or install `Axial.ErrorHandling` for all three. None installs Schema. Path-aware validation belongs
to Schema.

### Why Data Stays Here

`Axial.Data` provides source-neutral structured input values. `Schema.parse` consumes `Data` directly, and it has no
independent user journey outside of describing input to a schema, so it belongs in the Schema repository rather than
as a third leaf package alongside ErrorHandling.

### Schema Repository Dependency Graph

```text
Axial.Result ─────────────────────────┐
Axial.Check ───────────────────────────┼── Axial.ErrorHandling
Axial.Check ──── Axial.Refined ────────┘

Axial.Check ───▶ Axial.Schema
Axial.Refined ─▶ Axial.Schema
   ├── Axial.Schema.Json
   ├── Axial.Schema.JsonSchema
   ├── Axial.Schema.Testing
   ├── Axial.Schema.Http
   └── generated contract output

Axial.Schema.Contracts
        ↓ tool output targets Axial.Schema

Axial.Schema.Contracts.Build
        ↓ invokes the contract generator during MSBuild
```

`Axial.ErrorHandling` depends directly on Result, Check, and Refined — not only transitively through Refined → Check —
because it promises to install all three capabilities.

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
other Flow service packages
Flow examples
Flow benchmarks
Flow documentation and site
```

`Axial.Flow` must remain independent of `Axial.Result`, `Axial.Check`, `Axial.Refined`, and `Axial.Schema`.

Flow binds the standard F# `Result<'value, 'error>` and `Option<'value>` types directly. It does not need the
Result package to support typed failures.

Flow policies may accept ordinary functions returning standard `Result`. They must not create a package dependency on
Schema merely to provide convenience adapters.

## Cross-Product HTTP Adapters

`Axial.Schema.Http.AspNetCore` and `Axial.Schema.Http.GenHttp` currently depend on both Schema and Flow. They are
integration packages rather than evidence that the cores belong together.

Keep them in the Schema repository because their main responsibility is adapting schema-described HTTP boundaries.
They should consume released Flow packages through NuGet instead of project references.

The dependency becomes:

```text
released Axial.Flow packages
            ↓
Axial.Schema.Http.AspNetCore / GenHttp
```

These adapters should release only when their own code or dependency requirements change. A Flow release must not
automatically force a Schema repository release.

If cross-repository coordination becomes frequent, first reduce the adapter seam. Do not merge the repositories merely
to make atomic commits possible.

## The Umbrella Package

The current `Axial` umbrella ties Schema and the value/error-handling packages to one installation and apparent
release train. Remove it before 1.0 (still outstanding — see "Not Done Yet" above).

Keep the narrower `Axial.ErrorHandling` meta-package because it supplies a searchable category and a deliberate
complete installation for three related focused packages:

```bash
dotnet add package Axial.Result
dotnet add package Axial.Check
dotnet add package Axial.Refined
dotnet add package Axial.ErrorHandling
dotnet add package Axial.Schema
dotnet add package Axial.Flow
```

The meta-package contains no source API and should never be a dependency of Schema. Its README must name the three
packages it installs and show the namespace corresponding to each one. The focused packages remain the default in
examples that use only one capability.

## Package Versioning After The Split

Each repository should have an independent release train.

Packages within the Schema repository may initially share one version if coordinated releases remain convenient. The
same applies within the Flow repository.

Do not require the Schema and Flow repository versions to match.

Cross-product adapters should declare an explicit supported Flow version range. Test the lowest supported version and
the current version when practical.

Release notes should describe only the product and packages in that repository. Avoid a global Axial release note that
mixes unrelated changes.

## One Package Per Format

Future representation formats should use separate packages:

```text
Axial.Schema.Json
Axial.Schema.Xml
Axial.Schema.Yaml
Axial.Schema.Toml
Axial.Schema.MessagePack
Axial.Schema.Protobuf
```

This keeps transitive dependencies small and allows each format to have its own wire rules, limitations, runtime
support, release timing, and performance work.

Do not add empty packages in anticipation of demand. Create a package only when its format has an implemented consumer
and tests.

Do not create a public format-neutral package merely to hold interfaces. First prove that two or more formats share
substantial code with the same semantics.

If shared compiler machinery emerges, keep it internal to the repository or in an internal package until its boundary
is stable. Sharing the word "codec" is not enough reason for a public abstraction.

### Format Packages Are Not Interchangeable

JSON, XML, YAML, TOML, MessagePack, and Protobuf do not share the same data model.

Examples of format-specific differences include:

- object key and field-name rules;
- attributes versus elements in XML;
- aliases, anchors, and scalar resolution in YAML;
- table structure in TOML;
- integer widths and binary values in MessagePack;
- field numbers, unknown fields, and compatibility rules in Protobuf;
- streaming and framing behavior;
- canonical encoding and ordering;
- null, missing, optional, and default semantics.

Each package should state which Schema shapes and constraints it supports. Unsupported shapes should fail during codec
compilation with a typed error, not later while encoding a value.

## Shared Compiler, Platform-Specific JSON Runtime

`Axial.Schema.Json` should keep one public API and one schema-to-codec compiler.

The compiler walks Schema's retained typed shape and builds a reusable encoding and decoding plan. This logic should be
shared across .NET and Fable.

The runtime that executes the plan should be optimized for its platform.

```text
Schema<'value>
      ↓
shared JSON plan compiler
      ↓
platform runtime primitives
   ├── .NET UTF-8/span implementation
   └── Fable JavaScript implementation
```

Do not publish separate `.NET` and `JavaScript` NuGet packages at this stage. Platform selection is a compilation
detail, and users should write against the same `Json.compile`, serialize, and deserialize API.

## Fable Build Constraint

This repository cannot reliably select different F# source files for .NET and Fable compilation. Fable project
cracking has not made conditional file inclusion dependable.

Platform differences therefore must use inline compiler directives. Keep those directives concentrated in platform
modules rather than spreading them throughout codec compilation and parsing logic.

A file may define the same module twice, with only one implementation active:

```fsharp
#if FABLE_COMPILER
module internal JsonPlatform =
    // JavaScript implementation
#else
module internal JsonPlatform =
    // .NET implementation
#endif
```

Other files call `JsonPlatform` without their own compiler directives.

This follows the existing `Axial.Schema.Platform` pattern. The pattern is a response to the build constraint, not a
claim that .NET and JavaScript should use the same low-level representation.

## What Belongs In `Platform.fs`

Use a platform module for small operations that have the same meaning but different implementations:

- invariant integer and decimal parsing;
- UTF-8 string conversion;
- byte comparison and scanning;
- buffer rental and return;
- bounded byte slices;
- encoding string slices;
- exception construction where platform support differs;
- checks that depend on erased or retained runtime generic information.

Keep the call signatures platform-neutral when that does not damage the fast path.

Do not wrap every BCL call. A wrapper is useful when it removes a compiler directive from business or codec logic, or
when the operation requires different platform behavior.

## When To Use A Larger Conditional Runtime Module

Some differences are too large for a collection of tiny wrappers. In that case, place two implementations of a
coherent internal module behind one `#if` boundary in the same file.

Examples include:

- the input cursor;
- the output writer;
- JSON string escaping and unescaping;
- number parsing and formatting;
- property-name matching;
- stream integration;
- JavaScript-native string or typed-array integration.

The rest of the codec should depend on a small internal runtime surface. It should not know which implementation was
compiled.

Do not create one very large `Platform.fs` containing unrelated subsystems. Prefer focused modules such as
`JsonBufferPlatform`, `JsonNumberPlatform`, and `JsonTextPlatform` when the runtime grows.

## .NET JSON Runtime

The .NET implementation should operate directly on UTF-8 wherever the public input permits it.

Use appropriate .NET primitives such as:

- `ReadOnlySpan<byte>` for bounded parsing;
- `Span<byte>` for formatting into owned buffers;
- `Utf8Parser` and `Utf8Formatter` for supported primitives;
- `IBufferWriter<byte>` for caller-owned output;
- `ArrayPool<byte>` for temporary buffers;
- cached UTF-8 field names;
- direct stream or pipe adapters where they avoid intermediate strings.

Avoid converting a complete UTF-8 payload to `string` before parsing. Avoid allocating a new `byte[]` merely to pass a
slice when a span can represent it.

The current byte-array cursor is a useful portable baseline. The refactor should allow the .NET runtime to use spans
more directly without forcing span types into the shared public API or the Fable implementation.

Public .NET overloads may expose `ReadOnlySpan<byte>`, `ReadOnlyMemory<byte>`, `IBufferWriter<byte>`, `Stream`, or
`PipeReader` when each has a demonstrated use. Keep them behind `!FABLE_COMPILER` when Fable cannot represent them.

Do not make a ref-struct type part of a shared internal interface that Fable must compile.

## Fable JSON Runtime

The Fable implementation should use JavaScript's actual performance model rather than emulating .NET spans.

Candidate representations include JavaScript strings, `Uint8Array`, `TextEncoder`, and `TextDecoder`. Choose through
benchmarks and required interoperability, not by matching the .NET implementation mechanically.

If most Fable callers begin with a JavaScript string, a string-native decoder may be better than converting the entire
value to UTF-8 bytes first. If callers handle network or binary buffers, a typed-array path may be worthwhile.

The public behavior must match .NET for supported Schema shapes:

- field names and escaping;
- missing and unknown fields;
- duplicate-field policy;
- number ranges and failures;
- null and option semantics;
- discriminated union representation;
- map keys;
- date, time, GUID, and decimal formatting where supported;
- error paths and useful diagnostic text.

Identical implementation is not required. Equivalent documented behavior is required.

## Current Fable Status And Remaining Work

`Axial.Schema.Json` is a supported Fable surface. The benchmark uses the current Schema API,
`scripts/check-fable-js-surface.sh` passes, CI runs it, and generated JavaScript executes a Node encode/decode round
trip. Stream APIs remain .NET-only.

Further platform-runtime work should strengthen the shared semantic suite rather than re-prove basic support:

1. expand cross-platform golden cases for strings, numbers, nulls, options, lists, maps, records, and unions;
2. add decimal edge cases and reject syntax that differs unintentionally;
3. keep .NET-only APIs, such as streams, explicit in the documentation;
4. keep the Fable check in CI for every codec change.

## Performance Validation

Do not choose the platform abstraction from intuition alone. Benchmark the operations that dominate real payloads.

The .NET suite should measure:

- decode from UTF-8 bytes;
- decode from `ReadOnlySpan<byte>` where exposed;
- encode to caller-owned `IBufferWriter<byte>`;
- encode to string;
- stream encode and decode;
- allocation counts;
- field matching for small and large records;
- nested records, lists, maps, and unions;
- comparison with `System.Text.Json` source generation.

The Fable suite should measure:

- decode from string;
- decode from `Uint8Array` if supported;
- encode to string;
- encode to `Uint8Array` if supported;
- conversion cost between strings and UTF-8;
- comparison with native `JSON.parse` and `JSON.stringify` for equivalent behavior.

Keep platform-specific fast paths behind the same semantic tests. A faster implementation that accepts or emits a
different contract is a compatibility change, not an optimization.

## Focused Documentation Libraries

Result, Check, Refined, Schema, and Flow are focused documentation libraries, each with its own overview and API
reference index. Error Handling is the category and combined-installation page for the first three, not a
replacement for their identities. See "Completed So Far" for what is already live; the navigation below is the
target shape once repository extraction happens.

They may initially deploy from the current site infrastructure. A reader entering one library should encounter only
the dependencies and related concepts needed for that path.

### Result Documentation

```text
Axial Result
  Overview
  Getting started
  Result composition
  Computation expression
  API reference
```

Titles and descriptions should naturally include "F# error handling" for discovery.

### Check Documentation

```text
Axial Check
  Overview
  Getting started
  Reusable checks
  Check composition
  Predicates
  Check DSL
  API reference
```

The overview should say that checks return the standard F# `Result` and work with Axial.Result, FsToolkit.ErrorHandling,
or application-owned Result helpers.

### Refined Documentation

```text
Axial Refined
  Overview
  Getting started
  Parse representations
  Define refinements
  Built-in refined values
  Use checks in refinements
  API reference
```

Refined depends on Check but remains usable without Result or Schema.

### Schema Documentation

```text
Axial Schema
  Overview
  Getting started
  Parse structured input
  Construct domain models
  Checks and field constraints
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

- Schema field pages may link to Check for standalone reusable constraints and Refined for invariant-carrying values.
- Schema HTTP pages may say that handlers can return ordinary tasks or Axial Flow workflows.
- Flow pages may show a later example receiving a value admitted by Axial Schema.
- Each library home may link to the others under "Related Axial libraries."

The Error Handling category page may show the combined installation and route readers to Result, Check, or Refined.
Do not duplicate their guides there. The root landing page may show all five focused libraries while guiding newcomers
toward Result for simple code and Schema for structured boundaries.

## Documentation Deployment Options

The preferred final state is one documentation deployment per repository. Possible addresses include separate
subdomains or stable path prefixes.

Stable path prefixes fit the focused presentation without requiring separate deployments:

```text
axial.dev/error-handling
axial.dev/result
axial.dev/check
axial.dev/refined
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

The Flow repository must test without Schema or ErrorHandling references.

The Schema repository must test ErrorHandling, Schema, formats, contract tooling, and HTTP adapters. Flow-based HTTP
adapter tests should reference released Flow packages.

Add package-consumer tests that pack local artifacts and restore them into small fixture projects. This catches missing
package files, incorrect dependency ranges, build-target failures, and source-order problems.

Cross-product CI should include:

- the lowest supported Flow version for Schema HTTP adapters;
- the current stable Flow version;
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

### Phase 1 And 1B: Documentation And Package Surface

Completed 2026-07-21 through 2026-07-24 in the combined repository. See "Completed So Far" and "Not Done Yet" above
for what landed and what remains open before extraction.

### Phase 2: Concentrate Platform Differences

Not started.

1. Inventory every `#if` in the current codec.
2. Classify each branch as a small platform primitive, a coherent runtime subsystem, or a public .NET-only API.
3. Move small primitives into focused platform modules.
4. Place larger alternative implementations behind one conditional module boundary per subsystem.
5. Keep the shared schema compiler free of platform directives.
6. Preserve the passing Fable benchmark and Node round trip.
7. Expand cross-platform semantic golden tests.
8. Benchmark .NET span paths and JavaScript-native paths.
9. Optimize each backend without changing the shared behavior.

Do not block the repository split on every possible runtime optimization. Require a passing baseline and a design that
does not scatter new conditionals.

### Phase 3: Prepare Independent Builds

Not started.

1. Remove assumptions about one solution, one version property, one release note, and one docs site.
2. Change cross-product project references to package references in a migration branch.
3. Define adapter dependency ranges.
4. Create package-consumer fixtures (including the six scenarios listed under "Not Done Yet").
5. Decide where the combined reference application will live.
6. Verify that Flow builds with no Schema files and Schema builds against released Flow packages.

### Phase 4: Extract The Repositories

Not started. This is the actual repository split — proposed only, not performed.

1. freeze broad cross-product moves for the extraction window;
2. filter history into Schema and Flow repositories;
3. install repository-specific maintainer files and CI;
4. verify source inventories and generated paths;
5. publish prerelease packages;
6. deploy separate documentation entry points;
7. run the external reference application;
8. publish stable packages when consumer tests pass;
9. redirect the old repository and issue tracker.

## Acceptance Criteria

The split is complete when:

- Flow builds, tests, packs, documents, and releases without checking out Schema;
- Schema builds, tests, packs, documents, and releases without checking out Flow source;
- Schema's Flow-based HTTP adapters consume supported released Flow packages;
- `Axial.Result`, `Axial.Check`, and `Axial.Refined` are independently installable (done);
- `Axial.Refined` depends only on Check (done);
- `Axial.ErrorHandling` installs Result, Check, and Refined but contains no public API (done);
- `Axial.Schema` depends on Check and Refined directly, while Flow depends on none of them (done);
- the broad `Axial` umbrella no longer remains (not done);
- the Fable JSON executable check passes (done);
- .NET JSON fast paths use UTF-8 spans or caller-owned buffers where appropriate;
- platform directives are concentrated in internal platform/runtime modules;
- Result, Check, Refined, Schema, and Flow have distinct documentation identities (done at the reference-doc level;
  distinct nav sub-pages not done);
- NuGet, GitHub, and web searches for Result, F# error handling, validation, and diagnostics lead to the relevant
  packages or documentation;
- a clean consumer can install and run each product from published packages;
- the combined reference application works against published versions;
- repository instructions contain no stale paths or rules from the other product;
- release tags and notes no longer imply synchronized Schema and Flow versions.

## Risks And Mitigations

### Cross-Repository Changes Become Slower

Mitigation: keep the core seam small, use package dependency ranges, and test adapters against released versions.

### Integration Packages Lag Behind Flow

Mitigation: test the current stable Flow version in Schema CI and update only when an actual compatibility change occurs.

### Documentation Drifts

Mitigation: each repository owns its docs and references. Keep cross-links sparse and check them during deployment.

### Platform Abstraction Reduces .NET Performance

Mitigation: keep span-heavy work inside the .NET runtime module, benchmark allocations and throughput, and avoid
platform-neutral interfaces that require copying.

### Fable Behavior Silently Differs

Mitigation: run shared golden cases in .NET and generated JavaScript, especially for decimal, escaping, missing values,
and numeric ranges.

### History Extraction Obscures Changes

Mitigation: extract history first and make semantic changes in later ordinary commits.

## Decisions This Proposal Makes

- Two product repositories: Schema and Flow.
- Result, Check, and Refined remain focused package boundaries inside the Schema repository.
- `Axial.ErrorHandling` remains a dependency-only searchable meta-package; the `Axial` umbrella is removed.
- Flow remains independent of Result, Check, Refined, and Schema.
- Flow-based Schema HTTP adapters remain with Schema.
- The broad `Axial` umbrella package is removed; the focused ErrorHandling meta-package remains.
- Each future format gets its own package.
- One JSON package serves .NET and Fable.
- The schema-to-codec compiler is shared.
- Runtime implementations are platform-specific internally.
- Compiler directives are concentrated in platform modules because conditional source inclusion is not dependable.
- Documentation presents Result, Check, Refined, Schema, and Flow before source repositories are extracted.

## Choices To Resolve During Implementation

These choices do not change the main direction:

- final GitHub repository names and documentation URLs;
- whether the combined reference application receives its own repository;
- which .NET byte, memory, writer, stream, and pipe overloads belong in the first release;
- whether Fable's primary representation is string, `Uint8Array`, or both;
- whether Schema repository packages continue sharing a version after 1.0;
- whether Flow repository packages continue sharing a version after 1.0;
- the minimum Flow version supported by the Schema HTTP adapters.

Resolve these with consumer examples, package tests, and benchmarks. None requires returning to one combined repository
or one generic codec package.
