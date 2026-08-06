# Two Projects: Reified And Axial — Split And Documentation Plan

Status: planned work. Supersedes `project-split.md`'s three-repository and docs-shaping sections.

Everything below is work to do. Reasoning that led here is not repeated.

---

## 1. The Decisions

1. **Two independent projects**, not a family with an umbrella.
   - **Reified** — constraints, values, schema. Extracted to a new repository.
   - **Axial** — the effect system. Keeps this repository, its name, and its history; it is the trunk.
2. **No umbrella**: no third repository, no shared site, no mounts, no cross-repo `xref:`, no assembly
   pipeline. Each project has one repository and one site.
3. **The shared thesis is stated in prose, not encoded in package IDs.** Reified encodes invariants about
   values, boundaries, and models; Axial encodes invariants about computation — what can fail, what it
   requires, how it runs concurrently. One sentence on each site; no shared code.
4. **Navigation is by reader task, not by package**, in both projects.
5. **Generated reference is never committed** — it is produced at build.
6. **Nothing is published until the shape is settled.** Nothing with users has been released, so package
   boundaries and names are free to change today and breaking tomorrow. That window closes at first
   publish.
7. **The two pitches are deliberately asymmetric**, because the concepts land differently when explained.
   Reified leads with its concept — *the rule and its message are the same object, so they cannot drift* —
   because people immediately see why that is better. Axial does **not** lead with "effect system", which
   produces blank stares in most of .NET and much of F#. It leads with *easier async and Result*: failures
   in the signature, dependencies in the signature, swapped in a test. See `project-split.md`,
   "Positioning, per product".

---

## 2. Package Inventory And The Seam

Core dependency graph:

Names below are post-rename. Current source paths are still `Axial.*` throughout; see `project-split.md`
phase 9.

| Package | Depends on | Depended on by |
| --- | --- | --- |
| `Reified.Result` | — | nothing |
| `Reified.Parse` | — | Schema |
| `Reified.Constraint` | — | Refinements, Schema |
| `Reified.Refinements` | Constraint | Schema |
| `Reified.Data` | — | Schema |
| `Reified.Schema` | Constraint, Data, Parse, Refinements | — |
| `Axial` (the effect system) | — | the two server adapters below |
| `Reified.Schema.Http` | Schema | the two server adapters below |
| `Axial.AspNetCore` | **Axial**, Reified.Schema.Http, Schema.Json, Data | — |
| `Axial.GenHttp` | **Axial**, Reified.Schema.Http, Schema.Json, Data | — |

The two cores are independent. **One real seam exists**: the HTTP server adapters depend on both, and Flow
is in their public API rather than an implementation detail —

```fsharp
let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model>
(workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IResult>)
```

So the integration is: **declare a contract with Reified, serve it with Axial.**

`Schema.Http` is Flow-free — 406 lines describing endpoints and emitting an OpenAPI document via
`OpenApi.document : OpenApiInfo -> EndpointSpec list -> string`. Contract-first use (emit `openapi.json`,
generate clients, run contract tests) needs no server, so that layer goes with Reified and stands alone.

**Ownership after the split.** Description must not depend on execution, so the glue goes with the runtime:

| Today | After |
| --- | --- |
| `Axial.Schema.Http` | → `Reified.Schema.Http`, extracts with Reified |
| `Axial.Schema.Http.AspNetCore` | → `Axial.AspNetCore`, stays, depends on `Reified.Schema.Http` |
| `Axial.Schema.Http.GenHttp` | → `Axial.GenHttp`, stays, depends on `Reified.Schema.Http` |

(Top-level, as siblings of `Axial.HttpClient`. See `project-split.md` for the reasoning and the open
question about `Axial.*` versus `Axial.Hosting.*`.)

Reified then has zero knowledge of Axial. Axial carries two optional satellites that pull
`Reified.Schema.Http` only if used.

---

## 3. Work: Repository Split

### 3.1 Method — the description side is what gets extracted

Use `git filter-repo` (not `filter-branch`, which is deprecated; not a fork, which carries every unrelated
file's history forever and is marked as a fork by GitHub). Not currently installed —
`sudo dnf install git-filter-repo`.

**Which side gets filtered is settled by history.** The effect system is this repository's trunk — the
initial commit `d3b9617a` (2026-03-30) is the effect system, and the description packages do not appear
until `3a2a13f2` (2026-06-21). So:

- **Axial** — keeps this repository, its name, its history, and its tags. Removing the description paths is
  an ordinary `git rm` commit. **Do not `filter-repo` this repository**: it rewrites every SHA, breaks
  existing clones and branches, and orphans the v0.6.0 objects.
- **Reified** — fresh `git clone --no-local` into scratch, `filter-repo` down to the description paths,
  push to a new empty repository.

This inverts the earlier plan and removes its worst hazard: filtering the effect system would have had to
chase four historical renames (`EffectFs` → `EffectfulFlow` → `FlowKit` → `FsFlow*` → `Axial.Flow*`) and
would have silently dropped the v0.6.0 tag. Full detail in `project-split.md`, "Method — inverted".

### 3.2 Paths to extract

See `project-split.md`, "Paths to extract into Reified", which is the authoritative list, including the
historical `Axial.*` description names that `--path-glob` must also catch.

**Project assignments are resolved** — see `project-split.md` for the full classification of every example,
benchmark, and cross-cutting test project.

### 3.3 Order

1. Enumerate and verify the path list; confirm each ambiguous example's true dependencies.
2. Split the six "must split in two" projects, plus `Axial.Flow.Tests`, while both halves are in one tree.
3. Extract to Reified. **Do not rename in the same pass** — `--path-rename` would make history read as
   though it was always Reified. Extraction must be mechanically reviewable.
4. Confirm the extracted repository builds and its tests pass standalone.
5. Rename in ordinary commits, one repository at a time: `Axial.*` → `Reified.*` (and `Refined` →
   `Refinements`) there; `Axial.Flow*` → `Axial*` and the two adapters here.
6. Duplicate shared scaffolding into Reified: `Directory.Build.props`, `mise.toml`, CI workflows, test
   conventions, docs theme.
7. Only then remove the description paths from Axial.
8. Publish Reified first (Axial's adapters depend on `Reified.Schema.Http`), then Axial.

---

## 4. Work: Package Shape, Before First Publish

Free today, breaking after first publish.

1. **Deferred: merging `Reified.Constraint` into `Reified.Refinements`.** Not decided; Constraint stays
   separate for now. The clunkiness that motivated it is an API ergonomics problem that merging would not
   fix, so improve the standalone path first and see whether the premise survives. Free now, breaking after
   first publish — revisit before then. Note there is no payload argument either way: trimming and
   tree-shaking work on reachability, not package identity.
2. **Leave `Reified.Parse` alone.** Zero dependencies, a crisp standalone story, a self-explanatory name.
3. **`Refined` becomes `Refinements`.** Plural noun rather than past participle, so `Reified.Refinements`
   reads as adjective-plus-noun rather than two participles; it also agrees with the existing
   `Refinement.fs` and `Refinement<'input,'output>`. See `project-split.md`.
4. **Apply for NuGet prefix reservation**: `Reified.*` and `Axial.*`. Both verified free as of 2026-08-06.

---

## 5. Work: Documentation, Per Project

Folder name is the section name, so the IA is expressed by naming folders after reader tasks. Numeric
prefixes order them and are stripped from URLs.

The first five minutes are governed separately from the full information architecture. Flame's NuGet
README is the useful reference: it states the job in one sentence, installs, defines an ordinary F# record,
builds one schema, parses realistic JSON, uses the typed success value, and shows the failure shape before
expanding into rules or a catalogue. Its later README mixes tutorial, reference, internals, and benchmarks;
that is not the part to copy. Copy the speed and concreteness of the opening transaction.

For both projects:

1. **Complete one realistic transaction before explaining the architecture.** The reader must see familiar
   input become a useful typed result, or a familiar handler run successfully, before meeting the full
   vocabulary behind it.
2. **One route is visually dominant.** The landing page has one primary `Get started` action. Package
   matrices, overview tours, reference-app walkthroughs, and API reference must not compete with it.
3. **Show the payoff beside the declaration.** Do not ask the reader to carry boilerplate for several
   sections before showing what that declaration replaces or derives.
4. **Name concepts after the reader has observed them.** Terms such as interpreter, refinement, environment,
   and effect system explain behaviour already seen; they do not precede the first working example.
5. **Move catalogues out of the opening path.** Complete rule lists, parser tables, package inventories,
   performance details, AOT notes, and implementation paths belong in task guides, Reference, Packages, or
   Notes.

### 5.1 Reified — `./docs`

```
01-getting-started/
02-how-it-compares/          FluentValidation, DataAnnotations, Validus, Thoth,
                             FsToolkit, System.Text.Json
03-validating-values/        rules about a value
04-domain-types/             types that carry their rule
05-parsing-input/            Parse — decoding serialized primitives
06-modelling/                Schema — declaring a model
07-json/                     Schema.Json — codecs
08-http-contracts/           Schema.Http — endpoint declarations, OpenAPI output,
                             and how to serve them on plain ASP.NET
09-testing/                  Schema.Testing, Data — fixtures and test cases
10-notes/                    benchmarks, AOT and trimming detail
```

### 5.2 Axial — `./docs`

```
01-getting-started/
02-how-it-compares/          Polly, MediatR, plain Async/Task, IHostedService,
                             DI containers; ZIO and Effect for those who know them
03-the-flow-type/            creating, running, the flow { } builder
04-dependencies/             requirements, layers, services
05-error-handling/           the error channel; crossing from accumulated Results
06-concurrency-and-state/    Concurrency, Ref, STM
07-scheduling-and-retries/   Schedule, Policy
08-streams/
09-observability/            Telemetry (+JavaScript)
10-platforms-and-hosting/    providing an env and hooking it up
11-http/                     HttpClient, and serving Reified.Schema contracts
12-testing/                  fakes, layer swapping
13-notes/
```

Sections 6–9 are inferred from module names (`Schedule.fs`, `Policy.fs`, `Stm.fs`, `Stream.fs`, `Ref.fs`,
`Concurrency.fs`) rather than from what they teach. Open: whether `Schedule` and `Policy` are one topic or
two, whether `Stm` and `Ref` are public API worth teaching, whether `Stream` warrants its own section.

### 5.3 Getting started, one per project

**Reified** — declaration shaped: declare once, then derive. Lead with the concept; it sells when explained.

The page opens with one complete boundary transaction, not a definition of `Schema<'model>` and not the
package graph:

```text
ordinary F# model
    → explicit schema declaration
    → realistic untrusted input
    → typed success or path-aware accumulated errors
    → one derived output from the same declaration
```

Use an ordinary application shape such as signup, checkout, or configuration. Show the model, declaration,
input, `Schema.parse`, the successful value being used, and two failures together. Then derive exactly one
second artefact — preferably the JSON codec or JSON Schema — so the declaration pays for itself on the same
screen. Reified deliberately uses an explicit, reflection-free declaration rather than Flame's
`Schema.fromType`; the prose and example sequence must therefore make the return on that declaration
immediate. The first impression must not be "repeat the record fields and constructor now; learn why later."

After that completed transaction, widen from one value rule to the whole Reified story:

1. State the problem: the same rule is restated in a parser, a validator, a form, and a test.
2. Install only the package used by the example; defer the package matrix.
3. Declare one constraint and check a value with it.
4. Attach the declaration to a type so downstream code does not re-check it.
5. Return to the opening model and show how its fields consume the same declarations.
6. Derive the remaining codec, contract, and test fixtures from the model declaration.
7. Explain failures: an ordinary F# `Result` carrying a `Violation` or `ParseError` — Reified's
   types, derived from the rule, carrying context worth keeping. This is where the name earns itself: a
   `Constraint` carries its `ConstraintDescription`, and a violation carries the atom and the offending
   value as data, so the rule and its message cannot drift apart.
8. Route by the reader's next task, not by package.

Do not enumerate every interpreter before the first transaction finishes. `Data`, `SchemaErrors`, paths,
codec compilation, inspection, contracts, forms, OpenAPI, AOT, trimming, and Fable are all legitimate, but
introducing them before the reader has parsed one useful model turns the getting-started into an architecture
tour. The current material can be retained by moving each explanation after the complete example or into its
task guide.

**Axial** — root-type shaped, but problem-led rather than type-led. Effect opens with `Effect<A, E, R>` as
a type shape; the likeliest curious newcomer here is a C# developer who has never heard of ZIO or Effect,
and a three-parameter generic on page one will lose them. **Do not say "effect system" on this page** — see
§1 item 7. The reader should get most of the way through before the category is named at all.

1. The problem: a handler needs a database and can fail, and neither fact is in its signature
2. Install
3. Your first flow — write one, run it
4. Failure moves into the signature — the `'error` slot, contrasted with exceptions
5. Dependencies move into the signature — the `'env` slot, contrasted with constructor injection and DI
   containers
6. Putting it together with the `flow { }` builder
7. Swapping the dependency in a test — the payoff
8. Where to go next

Axial follows the same transaction-first rule: one handler, one expected failure, one explicit dependency,
one live run, then the same handler with a fake dependency. Concurrency, scheduling, layers, runtimes, and
the category name come later.

### 5.4 Landing page per project: route by symptom

Plain problem statements the destination page genuinely solves. Claim only what the library does —
observability is the honest promise; diagnosing a slow production system is not.

These symptom routes sit below the primary getting-started action. They are for a reader who already knows
which pain brought them here, not a replacement for the single newcomer path. Do not lead either landing
page with a package-card grid: that asks the reader to understand the product decomposition before seeing
the product work. Package inventory belongs in the Packages area.

**Reified**

| Problem | Goes to |
| --- | --- |
| Validation boilerplate is everywhere, and invalid values still get through | `03-validating-values/` |
| The same rule is repeated in a parser, a validator, a form, and a test | `06-modelling/` |
| Decoding and validation are separate steps that drift apart | `07-json/` |
| Client and server disagree about the shape of a request | `08-http-contracts/` |
| Constructing test data by hand is slow and repetitive | `09-testing/` |
| You want one small library, not a framework | Packages index |

**Axial** — note every row states a symptom, not a category. That is the positioning rule, not a stylistic
preference.

| Problem | Goes to |
| --- | --- |
| Code cannot be tested without a real database or HTTP call | `04-dependencies/` |
| Which failures a function can produce is not visible in its signature | `05-error-handling/` |
| Retry and timeout logic is written ad hoc at each call site | `07-scheduling-and-retries/` |
| Adding tracing or metrics means threading them through every function | `09-observability/` |
| The same logic has to run on the server and in the browser | `10-platforms-and-hosting/` |

### 5.5 Two pages to write first

Highest leverage, and neither depends on any tooling change.

- **`03-validating-values/`** opens with the constraint-derived failure: declare the rule once, get the
  check *and* its explanation, with `Violation` carrying identities and operands rather than prose. Today
  this is stranded at `weight: 40` inside a page titled "Localization", which reads as a niche concern for
  teams shipping multiple languages, when the primary benefit — no parallel catalogue of error messages to
  keep in step — applies to everyone shipping one. Localization becomes the proof, not the headline.
  *(Started: `docs/values/constraint/_index.md` rewritten in 994077b4.)*
- **`02-how-it-compares/`** leads with the same claim, because it is where FluentValidation and
  DataAnnotations are weakest: both maintain rules and messages separately, so drift is guaranteed.
  *(Started: `docs/values/constraint/comparison.md` added in 994077b4.)*

### 5.6 Also outstanding

- **141 dead cross-links.** `[text]({{< relref … >}})` renders as plain text with no anchor — verified
  against five variants: the `{{% … %}}` form, absolute links, relative links, and `relref` inside a raw
  HTML `href` all work. 141 occurrences across 53 files. Mechanical substitution, but may be moot if the
  FsLiveDocs migration lands first.
- **Stop committing generated reference.** `./docs` holds hand-written prose only. Renaming a section then
  moves a handful of files instead of hundreds, and `git log docs/` becomes a record of what was written.
- **Demote per-package `getting-started.md`** to quickstarts, or delete where they duplicate.
- **Move meta pages** (`packages-and-platforms`, `benchmarks`, `aot-trimming-fable`, comparisons) out of
  the learning path into notes.

---

### 5.7 Site structure: four areas, and three package tiers

Guides and reference are organised on orthogonal axes — guides by reader task, reference by code structure —
so they cannot be interleaved. They are separate top-level areas, as in Effect (Docs + API Reference), Rust
(the Book + docs.rs), and Django (topics + reference).

**Top nav: Docs · Reference · Packages · GitHub.**

- **Docs** — the task folders of §5.1.
- **Reference** — the generated entity tree, enriched per entity by hand-written prose (below).
- **Packages** — install matrix, dependency graph, standalone badges. Kept separate from Reference because
  "what do I install" is asked far more often than "what is the signature of X", and independent
  installability is the pitch.

FsLiveDocs already separates these: generated pages live at `/api/{entityId}.html`, `collectGuideOutputs`
excludes anything under `/api/`, `xref:` resolves to `api/{id}.html`, and `View.fs` already models
`overview` / `guides` / `api-docs` areas with labels and ordering. They are hardcoded and rendered as
sidebar groups rather than a top bar; a `navItem title url` helper already exists.

**Deep reference is authored, not just generated.** `ContentProvider.applyApiDocs` reads
`docs/api/{EntityId}.md` and substitutes it for that entity's generated summary:

```fsharp
let summary = docs |> Map.tryFind e.Id |> Option.defaultValue e.SummaryHtml
```

So any namespace, module, or type can carry a full authored page keyed by its entity id. With `<example>`
blocks verified against the real assembly and `{{< snippet >}}` transclusion, reference depth lives next to
the code and cannot drift.

**Three package tiers**, distinguished by what they are rather than by convenience:

Reified's packages; Axial's reference tiers its own the same way.

| Tier | Packages | API surface | Appears in |
| --- | --- | --- | --- |
| Core | `Result`, `Parse`, `Constraint`, `Refinements`, `Data`, `Schema` | yes | Reference, Packages |
| Schema extensions | `Schema.Json`, `Schema.Contracts`, `Schema.Http`, `Schema.Testing` | yes | Reference, Packages |
| Build tooling | `Schema.Contracts.Build` | **none** | Packages only |

`Schema.Contracts.Build` is `DevelopmentDependency=true`, `IncludeBuildOutput=false`, and compiles nothing —
it ships an MSBuild targets file and a generator. It must be excluded from Reference or it renders as an
empty entity.

Reference groups Core and Schema extensions separately. Docs does not tier at all: task folders cut across
the tiers, which is the point.

### 5.8 Namespace convention — settled

**Every package declares its own namespace equal to its package id.** You `open` the package you installed.
The cost is a stutter in the qualified name — `Reified.Result.Result`, `Reified.Data.Data` — which is
invisible at call sites, since consumers write `open Reified.Data` then `Data.assoc`. Accepted.

The A-versus-B debate that used to sit here is resolved and no longer repeated; `project-split.md`,
"Namespace convention", holds the mechanics — how `Data` moved, why `module Syntax` was promoted, and why
`module Json` stayed nested. Those carry over unchanged under the new names.

**The rule that nothing declares into the product root namespace now attaches to `Reified` only.** It
existed to stop `open Axial` becoming an unscoped catch-all across many unrelated value packages. Axial is
now a single product with a single root type, so `namespace Axial` holding `Flow<'env,'error,'value>` and
the `flow { }` builder is correct and desirable — `open Axial` gives exactly what was installed.

**Package identity in the reference model (§6 item 5) is still required.** Satellites like
`Reified.Schema.Json` and `Reified.Schema.Testing` share the `Reified.Schema.*` namespace prefix, so a
namespace tree can never say which NuGet a type ships in. The reference must state it explicitly.

---

## 6. Work: FsLiveDocs

Both projects use it as an ordinary consumer. Mounts, artifact packaging, and merged symbol tables are not
needed, since there is no merged site.

1. **Preserve folder structure in output paths.** The real prerequisite. `ContentProvider.fs:272` flattens
   every page — `Path.GetFileNameWithoutExtension(f).ToLowerInvariant() + ".html"` — so files discovered
   recursively via `SearchOption.AllDirectories` all collapse to the site root. `docs/guides/foo.md` becomes
   `/foo.html`, and same-named files in different folders collide silently. `collectGuideOutputs` (:148)
   flattens identically, and `validateLinks` builds its allowed-set from those names, so link validation
   changes with this.
2. **Folder-derived sections.** `View.fs:66-90` hardcodes a `guides` mapping for section id, display name,
   and order. Derive from folder name instead.
3. **Numeric prefix stripping** for ordering, in URLs and titles.
4. **Optional `_index.md` title override** per folder, for irregular casing ("JSON", "HTTP", "F#").
5. **Package identity in the model.** `PackageModel` is `{ Version; Entities; Scenarios }` — no package
   name — and `SymbolLister.merge` flattens N packages into one entity list, rebuilding the tree from
   namespace ids alone. With eleven packages in one build, the reference cannot tell a reader which NuGet a
   type ships in, and §5.8 shows namespace is not a reliable proxy. Carry a package name and tier through
   the merge, and display the package on every reference page.
6. **Areas as top nav, derived rather than hardcoded.** `View.fs:63-90` fixes `overview` / `guides` /
   `api-docs` with labels and ordering; derive them and render in the top bar (§5.7). A package with no API
   surface must be excludable from Reference entirely.

Items 1–4 are needed for the docs reorganisation. Items 5–6 are needed for the reference to be honest about
packaging, which matters more here than in a single-package project.

Do items 1–4 while the only consumer is FsLiveDocs' own small docs tree. Once Axial migrates, the same change
churns a large tree; doing it first means Axial migrates once, directly onto nested output.

---

## 7. Sequencing

This table tracks documentation work. `project-split.md` holds the authoritative split sequence; the two
agree, with different granularity.

| Phase | Work | Where |
| --- | --- | --- |
| 1 | FsLiveDocs items 1–4 | FsLiveDocs |
| 2 | Fold `Schema.JsonSchema` into `Schema`; unify the namespace convention | **done** |
| 3 | Verify split path list; split the seven cross-product projects | combined repo |
| 4 | `filter-repo` extract to Reified; confirm it builds green | new repo |
| 5 | Rename in each repository: `Axial.*` → `Reified.*` there, `Axial.Flow*` → `Axial*` here | both |
| 6 | Remove the description paths from Axial | Axial |
| 7 | Migrate docs to FsLiveDocs; stop committing generated reference | both |
| 8 | Reorganise into task folders (§5.1, §5.2) | both |
| 9 | Getting-starteds, landing pages, the two lead pages (§5.5) | both |
| 10 | Prefix reservations; publish Reified, then Axial | both |

Phases 2–3 are cheapest in the combined repository. Phase 8 touches nearly every docs file and should not
run concurrently with other docs work.

**Docs directories move with their product.** `docs/schema`, `docs/values`, `docs/result`, and `docs/data`
extract to Reified; `docs/flow` stays. `scripts/validate-*-docs.sh` split the same way, and
`scripts/generate-example-docs.sh` already takes a `schema|flow|all` product argument, so it splits cleanly
into one per repository.

---

## 8. Open Questions

1. **Is `Data` a Foundation or a Schema satellite?** It exists because building maps of lists by hand in
   tests and docs was miserable, which is a testing story. But it may be the easiest package to adopt
   first, which argues for prominence.
2. **Does a plain-ASP.NET serving path need a package?** After the split, Reified declares contracts and
   emits OpenAPI but ships no server. Write the "serve it on plain ASP.NET" page first (§5.1, folder 08);
   if the manual wiring turns out to be boilerplate people copy every time, it earns a package. If not, a
   page is the whole answer.
3. **Flow sections 6–9** — see §5.2.
4. **How long can Axial's getting-started avoid naming the category?** §1 item 7 says lead with symptoms,
   not "effect system". Where the phrase first appears, and whether `02-how-it-compares/` is the right place
   for it, is a writing decision to make when that page is drafted.
