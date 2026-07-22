# Repository, Package, And Documentation Split

Status: proposed repository direction. Documentation phase 1 was implemented and promoted to the durable decisions on
2026-07-21; the source/release repository split remains proposed.

This proposal separates Axial into products that can be understood, released, and used independently. It also defines
how .NET and Fable implementations should share an API without sharing the wrong runtime assumptions.

## Decision Summary

Axial now contains two main products:

1. Schema and ErrorHandling turn external input into useful application values and report why input was refused.
2. Flow describes and runs effectful work with explicit dependencies, expected failures, cancellation, and resources.

They should live in separate repositories. Their core packages already have no dependency in either direction.

`Axial.ErrorHandling` should remain a separate package but live in the Schema repository. Schema depends on it, and
the two evolve around the same parsing, diagnostic, constraint, and refined-value vocabulary.

Future formats should receive separate packages rather than being collected behind one generic codec package.

Schema and Flow should have separate documentation homes. Neither getting-started path should require knowledge of the
other product.

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

A **product** is the problem and documentation experience presented to users. A product may contain several packages.

A **format package** implements one external representation, such as JSON or MessagePack, over Schema declarations.

A **runtime backend** is the internal implementation chosen for a compilation platform. It does not imply a separate
public package.

## Target Repository: Axial Schema

The Schema repository owns the complete input-to-domain and domain-to-representation path.

Suggested repository name:

```text
Axial.Schema
```

It should contain:

```text
Axial.ErrorHandling
Axial.Data
Axial.Schema
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

### Why ErrorHandling Stays Here

`Axial.ErrorHandling` remains an independent leaf package. It must not depend on Schema or Flow.

It belongs in the Schema repository because Schema directly depends on its diagnostics and related value-handling
machinery. The user journey also treats `Result`, checks, validation, parsing, and refined values as parts of one path
from untrusted input to application values.

Putting ErrorHandling in a third repository would add another release and contribution boundary. It would not remove a
dependency because Schema would still consume it.

The repository placement must not blur the NuGet boundary. Users who only need `Result` helpers, checks, validation, or
refined values should still install `Axial.ErrorHandling` without installing Schema.

### Why Data Stays Here

`Axial.Data` provides source-neutral structured input values. `Schema.parse` consumes `Data` directly, and it has no
independent user journey outside of describing input to a schema, so it belongs in the Schema repository rather than
as a third leaf package alongside ErrorHandling.

### Schema Repository Dependency Graph

```text
Axial.ErrorHandling
        ↓
Axial.Schema
   ├── Axial.Schema.Json
   ├── Axial.Schema.Testing
   ├── Axial.Schema.Http
   └── generated contract output

Axial.Schema.Contracts
        ↓ tool output targets Axial.Schema

Axial.Schema.Contracts.Build
        ↓ invokes the contract generator during MSBuild
```

The contracts generator remains a tool-tier component. FSharp.Compiler.Service must not become a dependency of a
runtime package.

## Target Repository: Axial Flow

The Flow repository owns workflow description, execution, operational services, resource handling, and hosting.

Suggested repository name:

```text
Axial.Flow
```

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

`Axial.Flow` must remain independent of `Axial.ErrorHandling` and `Axial.Schema`.

Flow binds the standard F# `Result<'value, 'error>` and `Option<'value>` types directly. It does not need the
ErrorHandling package to support typed failures.

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

The current `Axial` umbrella package ties unrelated packages to one installation and one apparent release train.

Before 1.0, prefer removing it. The two product entry points should be explicit:

```bash
dotnet add package Axial.Schema
dotnet add package Axial.Flow
```

If an umbrella remains useful, move it to a very small release repository that references published packages. It must
not contain shared abstractions or require matching package versions.

Removing the umbrella is simpler and makes examples state what they actually use.

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
is stable. Sharing the word “codec” is not enough reason for a public abstraction.

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

## Current Fable Status And Required Work

The current codec contains Fable fallbacks and excludes stream APIs on Fable. Its source reaches Fable compilation, but
the repository's end-to-end Fable surface check currently fails because the benchmark uses removed Schema APIs.

Fable also warns that decimal parsing ignores `NumberStyles` and culture-provider arguments. That warning means the
current fallback must be tested for equivalent accepted syntax and range behavior before advertising full support.

Before claiming that `Axial.Schema.Json` is Fable-safe:

1. update the Fable benchmark to the current Schema builder API;
2. make `scripts/check-fable-js-surface.sh` pass;
3. run an encode/decode round trip in generated JavaScript;
4. add cross-platform golden cases for strings, numbers, nulls, options, lists, maps, records, and unions;
5. add decimal edge cases and reject syntax that differs unintentionally;
6. document APIs excluded from Fable, such as .NET streams;
7. run the Fable check in CI for every codec change.

The package may be described as designed for Fable before this work. It should be described as verified on Fable only
after the executable check passes.

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

## Separate Documentation Products

Schema and Flow should have separate documentation entry points, navigation, search context, examples, and API
reference indexes.

They may initially deploy from the current site infrastructure, but a reader entering one product should not encounter
the other product in the primary learning sequence.

### Schema Documentation

Suggested navigation:

```text
Axial Schema
  Overview
  Getting started
  Parse input
  Build domain values
  Result, checks, and diagnostics
  Refined values
  Application admission
  JSON
  Wire contracts and migrations
  HTTP boundaries
  Recommended patterns
  Testing
  API reference
```

ErrorHandling lives within this documentation experience but retains its package name. Explain on first encounter that
it can be installed and used without Schema.

The JSON pages should use the `Axial.Schema.Json` name and distinguish trusted codec decoding from full
`Schema.parse` diagnostics.

Future format packages should receive their own section only after they exist.

### Flow Documentation

Suggested navigation:

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

Flow examples should not introduce Schema as part of the basic path. Use ordinary inputs and standard F# `Result`.

### Cross-Links

Keep cross-links small and specific:

- Schema HTTP pages may say that handlers can return ordinary tasks or Axial Flow workflows.
- Flow pages may show a later example receiving a value admitted by Axial Schema.
- Each product home may link to the other under “Related Axial libraries.”

Do not maintain a combined getting-started guide. Do not present ErrorHandling as a third top-level product equal to
Schema and Flow.

## Documentation Deployment Options

The preferred final state is one documentation deployment per repository. Possible addresses include separate
subdomains or stable path prefixes.

Examples:

```text
schema.axial.dev
flow.axial.dev
```

or:

```text
axial.dev/schema
axial.dev/flow
```

Repository independence matters more than the URL shape. Each repository should be able to build and deploy its own
documentation without checking out the other.

If a shared landing site remains, keep it small. It should identify the two products and link to their documentation.
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

### Phase 1: Separate The Documentation Experience

Completed 2026-07-21:

1. Schema and Flow have independent home pages, guide trees, generated reference roots, agent pages, and machine-readable context.
2. ErrorHandling and Data live in the Schema learning path while retaining their independent-package explanations.
3. The root docs page is a short two-product index; product getting-started paths are self-contained.
4. Cross-product links are small and explicit.
5. Product-aware example/reference generators and validation commands establish build ownership before repository extraction.
6. Both product validation commands render the complete site and assert their entry and reference paths.

### Phase 2: Concentrate Platform Differences

1. Inventory every `#if` in the current codec.
2. Classify each branch as a small platform primitive, a coherent runtime subsystem, or a public .NET-only API.
3. Move small primitives into focused platform modules.
4. Place larger alternative implementations behind one conditional module boundary per subsystem.
5. Keep the shared schema compiler free of platform directives.
6. Update the Fable benchmark to the current Schema API.
7. Add cross-platform semantic golden tests.
8. Benchmark .NET span paths and JavaScript-native paths.
9. Optimize each backend without changing the shared behavior.

Do not block the repository split on every possible runtime optimization. Require a passing baseline and a design that
does not scatter new conditionals.

### Phase 3: Prepare Independent Builds

1. Remove assumptions about one solution, one version property, one release note, and one docs site.
2. Change cross-product project references to package references in a migration branch.
3. Define adapter dependency ranges.
4. Create package-consumer fixtures.
5. Decide where the combined reference application will live.
6. Verify that Flow builds with no Schema files and Schema builds against released Flow packages.

### Phase 4: Extract The Repositories

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
- `Axial.ErrorHandling` remains independently installable;
- `Axial.Schema` depends on ErrorHandling but Flow depends on neither;
- the Fable JSON executable check passes;
- .NET JSON fast paths use UTF-8 spans or caller-owned buffers where appropriate;
- platform directives are concentrated in internal platform/runtime modules;
- Schema and Flow have independent documentation homes and navigation;
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
- ErrorHandling remains a package boundary inside the Schema repository.
- Flow remains independent of Schema and ErrorHandling.
- Flow-based Schema HTTP adapters remain with Schema.
- The umbrella package should be removed unless a concrete use justifies a tiny independent release repository.
- Each future format gets its own package.
- One JSON package serves .NET and Fable.
- The schema-to-codec compiler is shared.
- Runtime implementations are platform-specific internally.
- Compiler directives are concentrated in platform modules because conditional source inclusion is not dependable.
- Documentation is separated by product before source repositories are extracted.

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
