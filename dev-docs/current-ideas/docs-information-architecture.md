# Documentation Information Architecture

Status: proposed. Supersedes the docs-shaping sections of `project-split.md`.

Axial's documentation is currently one Hugo site whose navigation mirrors the package graph:
**Result | Values | Data | Schema | Flow**, with `Values` grouping Constraint, Refined, and Parse. The
package split underneath is sound. The presentation is not.

This document proposes: **three repositories, one public site, task-based navigation within each product,
and FsLiveDocs as the engine** — with the root site holding a verified cross-product tutorial as its only
substantial content.

---

## 1. What Is Wrong Today

**The navigation is a picture of our package graph, not of the reader's problem.**

`Values` is the clearest symptom. It is explicitly not a package and not a namespace, so it exists purely
as a drawer, and `values/_index.md` opens by explaining that it isn't a thing. A navigation node whose own
page must apologise for itself is an artefact of how we build, not of how anyone reads.

Four consequences:

1. **Five peers, no through-line.** The landing page presents five equal doors. Nothing tells a newcomer
   where to start or which two of the five they actually need.
2. **Depth means different things in different places.** `result/` is 12 flat pages; `values/` is a
   grouping over three sub-sections with its own getting-started and tutorials; `flow/` is 20+ entries with
   its own sub-trees.
3. **Onboarding is taught five times.** Five `getting-started.md`, five `agent.md`, five `llms.txt`, plus
   overlapping `overview.md` / `what-it-does.md` / `packages-and-platforms.md`.
4. **Real tasks cross the boundaries.** "Accept untrusted JSON and get a domain type" touches Parse,
   Constraint, Refined, Result, and Schema — five sections, and no page that owns the story.

Separately, **generated reference markdown is committed to git**. Renaming one section moved hundreds of
files under `docs/*/reference/**`. None of it is authored content and it drowns the history of what was
actually written.

---

## 2. What Effect Does, And What Transfers

Effect ships many packages (`effect`, `@effect/platform`, `@effect/sql`, `@effect/ai`). Its documentation
barely mentions them. The sidebar is ~24 sections named for **problems** — Error Management, Requirements
Management, Resource Management, Observability, Configuration, Scheduling, Concurrency, Stream, Testing,
Schema, Platform. Packaging appears only at installation.

What transfers:

- **Navigate by task, not by artefact.** This is the central lesson and everything below answers to it.
- **A linear getting-started per mental model.** Effect's is eleven ordered pages. A reader who finishes it
  can read any other page.

What does not transfer:

- **One flat sidebar.** Effect's works because everything shares one type. We have two products with
  different shapes (§3), so we need a product level Effect does not.
- **Hiding packaging.** Effect's pitch is "stop installing a package for every problem." Ours is the
  opposite: independent installability is a genuine selling point. Packaging stays visible — as a lookup
  axis, not as the learning path.

---

## 3. Two Products, Two Topologies

Dependency graph as built:

| Package | Depends on | Depended on by |
| --- | --- | --- |
| `Axial.Result` | — | **nothing** |
| `Axial.Flow` | — | **nothing** |
| `Axial.Parse` | — | Schema |
| `Axial.Constraint` | — | Refined, Schema |
| `Axial.Refined` | Constraint | Schema |
| `Axial.Data` | — | Schema |
| `Axial.Schema` | Constraint, Data, Parse, Refined | — |

**Flow is a root-type product.** `Flow<'env, 'error, 'value>` is REA-shaped exactly like `Effect` and
`ZIO`. Concurrency, scheduling, streams, STM, telemetry, hosting, and platform services all hang off that
one type. Its large section is not sprawl — it is what a root-type product correctly looks like, and it
should be documented the way Effect documents itself: the type first, then chapters of capabilities.

**Schema is a declaration-and-derivation product.** It is *not* a pipeline — data does not travel through
stages. You declare a model once and codecs, contracts, JSON schema, validation, and documentation are
derived from that declaration. The shape is radial: one description at the centre, artifacts radiating out.

The `Parse → Constraint → Refined` arrow seen in examples is one function's control flow, not the
product's architecture, and must not become the section's organising metaphor.

**Ladder within the Schema product** — these are not the same rung:

- `Constraint` — rules about a single value
- `Refined` — a single value *type* carrying its rule
- `Schema` — the shape of a whole model, and everything derived from it

**They meet at F#'s own `Result`, as peers.** `Axial.Flow`'s builder carries overloads lifting
`Result<'value,'error>`, `Async<Result<…>>`, `Task<Result<…>>`, and `ValueTask<Result<…>>` straight into a
`Flow` (`BindError.fs`). Neither depends on the other's packages; neither is subordinate.

Effect grew Schema later, onto a world already rooted in the effect type, so its Schema is subordinate to
something it has no intrinsic relationship with. Axial never made that move and should not retrofit it.

---

## 4. What Unifies Axial — And What Does Not

**Not `Result`.** It is stdlib, every well-behaved F# library returns it, and it therefore distinguishes
nothing. It is the reporting convention that lets independently built packages compose. Worth one clear
page; not an identity.

**Not a shared runtime story.** Every candidate for substantial cross-product documentation evaporated
under scrutiny:

- *Testing* — two tasks sharing a word. Testing a schema (codec round-trips, fixtures) and testing a flow
  (faking services, swapping layers) share no vocabulary. A root page would be a disambiguation stub.
- *Error handling* — real, but it belongs inside the products rather than at the root. Note the error type
  is **not** simply "yours": `Constraint` and `Refined` return `Violation`, `Parse` returns `ParseError` —
  those are given. Only `Flow`'s `'error` slot and `Result`'s are polymorphic. So the crossing is
  specifically *Axial-supplied error types becoming your error type*, and it carries two non-obvious
  decisions: **accumulation does not survive it** (the admitting side collects every error, `Flow` has a
  single fail-fast channel, so where you collapse is a design choice), and **mapping too early discards
  structure** — see below. Both belong in Schema's validating-values and Flow's error-handling sections.

  What that structure is deserves stating prominently, because it is one of the strongest arguments in the
  library and is currently buried in a localization page. **The failure is derived from the constraint, so
  there is no parallel catalogue of error messages to maintain.** Declare the rule once and you get the
  check *and* its explanation. A `Violation` carries no language — only identities and operands, which is
  why it stays comparable data you can retain, test against, and pass across a boundary without dragging a
  culture or a `ResourceManager` with it. Prose happens at the rendering edge:

  ```fsharp
  let field = renderer |> Renderer.context "signup" |> Renderer.attribute "name"
  violation |> Violation.message field      // "must be present"
  violation |> Violation.fullMessage field  // "Name must be present"
  ```

  Full i18n falls out of the same split — `Renderer.ofResourceManager resources culture` renders "Le nom
  doit être renseigné" from the identical violation, with contextual fallback, and no application code
  walks a violation tree or reproduces Axial's key catalogue. Mapping a `Violation` into a hand-rolled DU
  too early throws all of this away, which is precisely why the timing of the conversion matters.
- *HTTP* — the strongest, and still one page: `Schema.Contracts` lets you declare an endpoint contract once
  and both serve it and call it.

**So Axial is a brand family with a shared method**, not a framework. The method is *reified description*:

| Package | Is a value describing… | Consumed by… |
| --- | --- | --- |
| `Constraint` | which values are acceptable | checking, **and the failure it produces** — message rendering and i18n, Fable portability |
| `Schema` | how input becomes a model | codec, contract, JSON schema, validation, docs |
| `Flow` | work, its requirements, and its failures | a runtime that executes it |

Schema's description is **derived from**; Flow's description is **run**. Same method, two consumers. This
generalises the existing headline — *encode each invariant once, enforce it across the project* — past
"invariant" to cover Flow, without claiming a shared type that does not exist.

The positioning line:

> Effect asks you to adopt one type. Axial's libraries interoperate because they speak the one F# already
> has.

---

## 5. Target Structure: Three Repositories, One Site

| Axis | Count | Detail |
| --- | --- | --- |
| Repositories | 3 | `Axial.Schema`, `Axial.Flow`, `Axial` (umbrella) |
| Docs builds | 3 | each product builds standalone for CI and local preview; umbrella builds the merged one |
| **Public sites** | **1** | `axial.dev` — the merged build |

Product repos run `livedocs test` and `livedocs build` in their own CI. That is how examples get verified
next to the code they document, and how you preview without checking out three repos. Those builds are not
what the public reads.

**Ownership:**

- `Axial.Schema` / `Axial.Flow` — their packages, their prose, their verified examples. Content lives with
  the code it documents, so it cannot drift.
- `Axial` — landing page, the cross-product tutorial, mount manifests, theme, and the CI that assembles
  and deploys. **Assembly and presentation, not content.**

URL shape:

```
/                → landing page + tutorial      (Axial)
/schema/…        → mounted                      (Axial.Schema)
/flow/…          → mounted                      (Axial.Flow)
```

One site is required, not merely preferred: `xref:` resolution needs both symbol packages in one build.
Edge routing between two separately deployed sites cannot give cross-product references or one search
index.

---

## 6. Navigation: Tasks Within Each Product

Folder name *is* the section name (§7.1). So the IA is expressed by naming folders after reader tasks.
Numeric prefixes give ordering and are stripped from URLs.

**`Axial.Schema` — `./docs`**

```
01-getting-started/
02-how-it-compares/          FluentValidation, DataAnnotations, Validus, Thoth,
                             FsToolkit, System.Text.Json
03-validating-values/        Constraint — rules about a value
04-domain-types/             Refined — types that carry their rule
05-parsing-input/            Parse — decoding serialized primitives
06-modelling/                Schema — declaring a model
07-json/                     Schema.Json — codecs
08-http-contracts/           Schema.Http, Schema.Contracts
09-testing/                  Schema.Testing, Data — fixtures and test cases
10-notes/                    benchmarks, AOT and trimming detail
```

Fable and AOT/trimming get **no section of their own here**. For Schema they are an expectation to state
once — a page or a paragraph in `01-getting-started/` saying what runs where — and otherwise a note on the
pages where they actually bite. Giving them a folder would imply a body of work that does not exist.

**Comparisons sit at position 02, not in the notes.** For anyone evaluating — particularly C# developers
arriving from FluentValidation or DataAnnotations, and F# developers already using FsToolkit or Thoth —
the comparison *is* the entry point, not an appendix. It has to be specific and unafraid: where the
alternative is a better fit, say so.

**Two pages carry more weight than the rest and should be written first.**

- `03-validating-values/` opens with the constraint-derived failure (§4): declare the rule once, get the
  check *and* its explanation, with `Violation` carrying identities and operands rather than prose. This is
  the most legible demonstration of the shared method anywhere in Axial, and today it is stranded at
  `weight: 40` inside a page titled "Localization" — which reads as a niche concern for teams shipping
  multiple languages, when the primary benefit (no parallel catalogue of error messages to keep in step)
  applies to everyone shipping one. Localization becomes the *proof*, not the headline.
- `02-how-it-compares/` leads with that same claim, because it is exactly where FluentValidation and
  DataAnnotations are weakest: both make you maintain rules and messages separately, and drift is
  guaranteed. i18n follows as a consequence of the design rather than as a feature that had to be built.

**`Axial.Flow` — `./docs`**

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
09-observability/            Flow.Telemetry (+JavaScript)
10-platforms-and-hosting/    providing an env and hooking it up: Hosting.Browser/Node,
                             PlatformService, Process, FileSystem
11-http-clients/             Flow.HttpClient
12-testing/                  fakes, layer swapping
13-notes/
```

Flow's platform section is genuinely a section, unlike Schema's: it is about *providing an environment and
wiring it up*, which is substantial work with real packages behind it, not a portability caveat.

Sections 5–8 of the Flow list are inferred from module names (`Schedule.fs`, `Policy.fs`, `Stm.fs`,
`Stream.fs`, `Ref.fs`, `Concurrency.fs`) rather than from what those modules teach. Open for review:
whether `Schedule` and `Policy` are one topic or two, whether `Stm` and `Ref` are public API worth teaching
or internal machinery, and whether `Stream` warrants its own section.

### 6.1 The two getting-starteds

There is no single onboarding path, because there is no single mental model (§3).

**`01-getting-started/` for Schema** — declaration shaped: declare once, then derive.

1. The problem: the same rule restated in a parser, a validator, a form, and a test
2. Install what you need
3. Declare one constraint; check a value with it
4. Attach the declaration to a type so nothing re-checks it (`Refined`)
5. Declare a whole model; derive the codec and contract from it
6. Derive test fixtures from the same declaration (`Data`)
7. How failures are reported: an ordinary F# `Result` carrying a `Violation` from a constraint or a
   `ParseError` from a parser. The failure comes *from* the rule you declared in step 3 — no second
   catalogue of error messages to keep in step — and renders to a sentence at the edge, in any language.
8. Where to go next

**`01-getting-started/` for Flow** — root-type shaped, but problem-led rather than type-led.

1. The problem: a handler needs a database and can fail, and neither fact is in its signature
2. Install
3. Your first flow — write one, run it
4. Failure moves into the signature — the `'error` slot, contrasted with exceptions
5. Dependencies move into the signature — the `'env` slot, contrasted with constructor injection and DI
   containers
6. Putting it together with the `flow { }` builder
7. Swapping the dependency in a test — the payoff, and the reason steps 4 and 5 were worth it
8. Where to go next

**Flow's sequence deliberately departs from Effect's.** Effect opens with `Effect<A, E, R>` as a type
shape, which works for readers who already accept that framing. Axial's likeliest curious newcomer is a C#
developer who has never heard of ZIO or Effect, and a three-parameter generic on page one will lose them.
So the slots are introduced one at a time, each as the answer to a problem the reader already has, with the
test-swapping payoff arriving early enough to justify the setup. ZIO and Effect appear in
`02-how-it-compares/` for those who know them; they are never assumed in the onboarding path.

Per-package `getting-started.md` files are demoted to short quickstarts, or deleted where they duplicate
their product's path.

The naming problems that dominated earlier drafts — `Values`, `Modelling`/`Executing`, `Foundations`,
where `Data` goes, whether `Result` is top-level — all disappear. Task folders have no drawers. `Data`
appears under `08-testing/`; `Result` appears wherever composition is being taught.

**Packages remain a second axis, for lookup.** A `Packages` index plus the generated reference, organised
by the tiers the package names already encode:

| Tier | Named | Members |
| --- | --- | --- |
| Flagship | `Axial.X` | `Axial.Schema`, `Axial.Flow` |
| Satellite | `Axial.X.Y` | Schema.Json, Schema.Contracts, Schema.Http, Schema.Testing, Flow.Console, Flow.Hosting, Flow.Telemetry, … |
| Substrate | `Axial.Y` | Constraint, Refined, Parse, Data, Result |

A satellite's name says it is a part; a substrate package's name deliberately does not — `Axial.Constraint`
was never `Axial.Schema.Constraint`, because it stands alone. Counting `src/`: the Flow side is 12
packages, the Schema side 9 plus 5 substrate. The sides are balanced; earlier sketches only looked lopsided
because Flow's satellites collapse visually inside their own names.

Every task page opens with the install line it needs. Substrate packages carry a "works standalone — no
other Axial package required" badge.

---

## 7. FsLiveDocs

The engine moves from Hugo to FsLiveDocs (`../../FsLiveDocs`), which brings verified examples
(`/// <example>` extracted and run against the real project), snippet transclusion from `.fs` files,
semantic `xref:`, JSON symbol snapshots, Pagefind search, and generated `llms.txt`.

### 7.1 Guiding principle

**Transparent structure: the folder name is the nav.** Augment with frontmatter only where unavoidable.
Docs live in `./docs`, independent of namespace, module, and package layout.

Two additions that stay folder-native:

- **Ordering** — numeric prefixes (`03-validating-values/`), stripped from URL and title.
- **Titles with irregular casing** — derived titles cannot produce "JSON", "HTTP", or "F#". An optional
  `_index.md` in the folder may override the title, appearing only where actually needed.

### 7.2 Mounts

A mount is declared by a `.toml` file in `./docs` **named after its mount point**:

```toml
# docs/schema.toml   →  /schema
repo    = "adz/Axial.Schema"
version = "0.8.3"

# for local development instead:
# path  = "../Axial.Schema"
```

The filename gives the mount point, so the naming principle extends unchanged from folders to mounts, no
placeholder folder is needed in git, and every mount is visible in one `ls ./docs`. Nesting follows the
path: `docs/libraries/schema.toml` → `/libraries/schema`.

Rules: error if `docs/schema.toml` and `docs/schema/` both exist; exclude `.toml` from content rendering.

**A mounted repo is unaware of its mount point.** The same source builds standalone at `/` in the product
repo and at `/schema` in the umbrella. This requires:

- `xref:` — symbolic, resolved against the merged symbol table; mount-independent by construction.
- Relative links between pages — fine.
- **Absolute internal links must fail the build**, rather than 404 silently.
- Assets resolved mount-relative.

### 7.3 Artifacts, not source

A mount needs symbols as well as prose, or `xref:` and reference pages break at the boundary. Compiling
both product repos in the umbrella's CI would recouple the repositories the split just separated.

Instead, each product release publishes an artifact — its `./docs` prose plus its extracted symbol JSON
(`livedocs extract`). The umbrella consumes that. Its build is markdown plus JSON: no MSBuild, no F#
compilation of other repos, and versioning falls out of which artifact is pinned.

**Generated reference markdown is never committed.** `./docs` holds hand-written prose only; reference is
generated at build from the projects and snapshotted at release. Renaming a section then moves a handful of
prose files instead of hundreds of generated ones, and `git log docs/` becomes a record of what was
written.

### 7.4 What FsLiveDocs needs — this migration is its 1.0 proving ground

1. **Preserve folder structure in output paths.** This is the real prerequisite and everything else rests
   on it. `ContentProvider.fs:272` currently flattens every page —
   `Path.GetFileNameWithoutExtension(f).ToLowerInvariant() + ".html"` — so files discovered recursively via
   `SearchOption.AllDirectories` all collapse to the site root. Today `docs/guides/foo.md` becomes
   `/foo.html`, same-named files in different folders silently collide, and `/schema/…` prefixes are not
   expressible. `collectGuideOutputs` (:148) flattens identically, and `validateLinks` builds its
   allowed-set from those names, so link validation changes with this.
2. **Folder-derived sections.** `src/FsLiveDocs.Renderer/View.fs:66-90` hardcodes a `guides` mapping for
   section id, display name, and order. Replace with derivation from folder name.
3. **Numeric prefix stripping** for ordering, in URLs and titles.
4. **Optional `_index.md` title override** per folder.
5. **Mounts** — `.toml` named after mount point; `path` for dev, `repo` + `version` for CI. `SiteConfig`
   (`.livedocs/config.json`) exists but carries only `RepoUrl`, so it is an alternative home for mount
   declarations if per-folder `.toml` files prove awkward.
6. **Merged symbol packages across mounts** so `xref:` resolves globally. Largely plumbing:
   `getUnifiedPackage` extracts per project then calls `SymbolLister.merge` over a `PackageModel list`,
   which is indifferent to where those models came from — feeding it deserialised snapshots should work.
7. **Mount-relative link resolution**, with absolute internal links failing the build.
8. **Artifact publish/consume** — a `livedocs pack` producing prose + JSON, and mount resolution that
   downloads and unpacks it. The serialisation already exists: `livedocs extract` emits the JSON blob and
   `FsLiveDocs.Core.Serialization.jsonSettings` is the shared settings.

---

## 8. Release And Deployment

**Product release** (`Axial.Schema`, tag `v0.8.3`):

1. Build, test, `livedocs test` — examples verified against the real assemblies.
2. `livedocs extract` → symbol JSON.
3. Pack `./docs` prose + JSON → `livedocs.zip`, attached to the GitHub release.
4. Push NuGet packages.
5. `gh` opens a PR against `Axial` bumping `docs/schema.toml` to `0.8.3`.

**Umbrella build** (on merge to `main`):

1. Read every `*.toml` under `./docs`.
2. `gh release download <version> --repo <repo> --pattern livedocs.zip` per mount; unpack to temp dirs.
3. `livedocs build` over the umbrella's own prose plus the mounted trees, merging symbol packages.
4. Deploy the static output.

**Worked example.** Tagging Schema `0.8.3` while Flow stays at `0.7.0`: `schema.toml` moves to `0.8.3`,
`flow.toml` is untouched. The build fetches Schema's new artifact and Flow's unchanged one and deploys
both; the `/flow` subtree comes out byte-identical. Flow being stale is the normal case, not an edge case.

**Pinned, not floating.** The umbrella's git history is then an exact record of what is deployed, and any
commit rebuilds to the same site. Floating "latest" means the same commit builds differently next month and
past states cannot be reproduced. The cost is one automated PR per release, which doubles as the review
point before docs go live.

**Prose-only fixes** should not require a NuGet bump. Cheapest answer: a docs-only tag (`v0.8.3-docs.1`)
that republishes the artifact without publishing packages; the mount pins that tag.

---

## 9. The Root Site

Deliberately thin:

- **Landing page — routes by symptom, not by product name.** This is the correction that matters most for
  acquisition. Making `Schema | Flow` the reader's first choice reinstates the defect this whole document
  set out to remove: it is an artefact name, and for four of the five substrate packages it actively
  misleads. Someone who wants a string validator, a `Result` computation expression, or test fixtures
  should not have to click a thing called "Schema", which reads as JSON serialization.

  Mounts are a build-and-URL mechanism, not a presentation constraint. URLs stay `/schema/…` and
  `/flow/…`; the landing page never says so. It lists symptoms:

  | Problem | Goes to |
  | --- | --- |
  | Validation boilerplate is everywhere, and invalid values still get through | `/schema/03-validating-values/` |
  | The same rule is repeated in a parser, a validator, a form, and a test | `/schema/06-modelling/` |
  | Decoding and validation are separate steps that drift apart | `/schema/07-json/` |
  | Client and server disagree about the shape of a request | `/schema/08-http-contracts/` |
  | Constructing test data by hand is slow and repetitive | `/schema/09-testing/` |
  | Code cannot be tested without a real database or HTTP call | `/flow/04-dependencies/` |
  | Which failures a function can produce is not visible in its signature | `/flow/05-error-handling/` |
  | Retry and timeout logic is written ad hoc at each call site | `/flow/07-scheduling-and-retries/` |
  | Adding tracing or metrics means threading them through every function | `/flow/09-observability/` |
  | The same logic has to run on the server and in the browser | `/flow/10-platforms-and-hosting/` |
  | You want one small library, not a framework | Packages index |

  Keep these plain and claim only what the library does. Each row must name a problem a reader recognises
  and that the destination page genuinely solves — not a consequence several steps removed from it.
  Observability is the honest promise; diagnosing a slow production system is not.

  The last row matters more than its position suggests: it is the entry point for basic F# users and the
  only place the "works standalone" promise is visible before commitment.

  Below the symptom table, briefly: the two products, the shared method (§4), one snippet each. The page
  directs; it does not teach.
- **Packages index** — the three tiers, for lookup.
- **A deep, verified cross-product tutorial** — later work, but the umbrella's flagship. Under FsLiveDocs
  its examples are extracted and run against both products' real assemblies, with snippets transcluded from
  a working sample project. A tutorial that provably compiles against both libraries is a stronger argument
  that they are a family than any prose framing, including §4.

If that tutorial turns out to be hard to write, the family thesis is decoration rather than structure —
worth knowing, and the honest fallback ("two libraries, same design principles") is still a fine position.

---

## 10. Sequencing

| Phase | Work | Where |
| --- | --- | --- |
| 1 | FsLiveDocs §7.4 items 1–4 — nested output paths, folder-derived sections, ordering, title override | FsLiveDocs |
| 2 | Migrate the combined repo's docs to FsLiveDocs; retire Hugo and `site/` | Axial |
| 3 | Stop committing generated reference; generate at build | Axial |
| 4 | Reorganise into task folders per §6, still in one repo | Axial |
| 5 | FsLiveDocs §7.4 items 5–8 — mounts, merged symbols, links, artifacts | FsLiveDocs |
| 6 | Split repositories; add mount manifests and release workflows | all three |
| 7 | Write the two getting-starteds; demote per-package ones to quickstarts | products |
| 8 | Landing page and Packages index | Axial |
| 9 | The verified cross-product tutorial; tag FsLiveDocs 1.0 | Axial |

Phases 1–4 happen in the combined repository, which is far cheaper than doing them across three. Phase 4
touches nearly every docs file and should not run concurrently with other docs work.

Two items from earlier drafts are **dropped**: consolidating `agent.md` / `llms.txt` (FsLiveDocs generates
`llms.txt`), and normalising a `reference/` folder per section (reference is generated).

---

## 11. Open Questions

1. **Prose-only release cadence.** Does the docs-only tag (§8) suffice, or does typo latency justify
   letting a mount track a branch for prose while symbols come from the pinned release? The latter splits
   the artifact in two; avoid unless the pain proves real.
2. **Does `Data` sit under testing, or earn its own task section?** It exists because building maps of
   lists by hand in tests and docs was miserable, which is a testing story. But it may be the easiest
   package to adopt first, which argues for prominence.
3. **Does the root tutorial justify the umbrella?** §9's test. Until it is written, the umbrella is a
   landing page, a merged search index, and cross-product `xref:` — which may be enough on its own.

---

## Appendix: Rejected Alternatives

- **Nav grouped as `Values`** — a label whose page must explain it is not a package. The origin of this
  document.
- **Nav grouped by verbs** (`Admit` / `Compose` / `Describe` / `Execute`) or **gerunds**
  (`Modelling` / `Executing`) — collision-free, but still a package tree with better labels, and both bury
  the flagships one level down.
- **A single site-wide getting-started** — assumes one mental model. There are two (§3).
- **One spine `Parse → Constraint → Schema → Flow`** — Effect-mimicry. It makes Flow the terminal station
  of a pipeline it is not part of and implies a subordination absent from the code.
- **Extracting `Axial.Result` as its own product** — it is the one package serving neither thesis, but
  alone it is a small library against an entrenched incumbent (FsToolkit.ErrorHandling) with no umbrella to
  lend credibility. Under the umbrella it is the cheapest on-ramp.
- **Union-merging same-named folders across repos** — designed for cross-product task sections that turned
  out not to exist (§4). Mounts are simpler and keep ownership obvious.
- **Two separately deployed sites with edge routing** — cheapest to operate, but forfeits cross-product
  `xref:` and a single search index.
