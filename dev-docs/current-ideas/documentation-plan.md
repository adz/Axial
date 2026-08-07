# Documentation plan

Unaccepted sketch. Target information architecture for `docs/`, plus the tooling changes it depends on.
Nothing here is implemented.

The governing rule is `dev-docs/decisions/README.md`, "Do not lead with effect system". Everything below is
that rule applied to structure.

## Where the docs are now

`docs/flow/` holds the whole user-facing tree, with `comparisons/`, `concurrency/`, `console/`,
`core-concepts/`, `filesystem/`, `getting-started/`, `hosting/`, `http/`, `platform-service/`, `processes/`,
`services-and-runtimes/`, `telemetry/`, `tutorials/`, and loose pages (`applications.md`,
`observability.md`, `overview.md`, `packages-and-platforms.md`).

The `flow/` level is a leftover from the combined repository, where it distinguished this product from
`schema/`, `values/`, `result/`, and `data/`. With one product it is a redundant path segment on every URL,
and the sections beneath it are named after packages and modules rather than after anything a reader wants
to do.

## Target sections

Folder name is the section name, so the architecture is expressed by naming folders after reader tasks.
Numeric prefixes order them and are stripped from URLs. `docs/flow/` collapses into `docs/`.

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
11-http/                     HttpClient, and serving externally declared HTTP contracts
12-testing/                  fakes, layer swapping
13-notes/                    benchmarks, AOT/trimming/Fable detail
```

Sections 6–9 are inferred from module names (`Schedule.fs`, `Policy.fs`, `Stm.fs`, `Stream.fs`, `Ref.fs`,
`Concurrency.fs`) rather than from what they teach. Open: whether `Schedule` and `Policy` are one topic or
two, whether `Stm` and `Ref` are public API worth teaching, whether `Stream` warrants its own section.

## The first five minutes

Governed separately from the rest of the architecture:

1. **Complete one realistic transaction before explaining the architecture.** The reader must see a familiar
   handler run successfully before meeting the vocabulary behind it.
2. **One route is visually dominant.** The landing page has one primary `Get started` action. Package
   matrices, overview tours, reference-app walkthroughs, and API reference must not compete with it.
3. **Show the payoff beside the declaration.** Do not ask the reader to carry boilerplate for several
   sections before showing what it buys.
4. **Name concepts after the reader has observed them.** Environment, layer, fiber, effect system explain
   behaviour already seen; they do not precede the first working example.
5. **Move catalogues out of the opening path.** Complete operator lists, package inventories, performance
   detail, AOT notes, and implementation paths belong in task guides, Reference, Packages, or Notes.

## Getting started

Root-type shaped, but problem-led rather than type-led. Effect opens with `Effect<A, E, R>` as a type shape;
the likeliest curious newcomer here is a C# developer who has never heard of ZIO or Effect, and a
three-parameter generic on page one will lose them.

1. The problem: a handler needs a database and can fail, and neither fact is in its signature
2. Install
3. Your first flow — write one, run it
4. Failure moves into the signature — the `'error` slot, contrasted with exceptions
5. Dependencies move into the signature — the `'env` slot, contrasted with constructor injection and DI
   containers
6. Putting it together with the `flow { }` builder
7. Swapping the dependency in a test — the payoff
8. Where to go next

Open: where "effect system" first appears at all, and whether `02-how-it-compares/` is the right place.

## Landing page: route by symptom

Plain problem statements the destination page genuinely solves. Every row is a symptom, not a category.
Claim only what the library does — observability is the honest promise, diagnosing a slow production system
is not.

| Problem | Goes to |
| --- | --- |
| Code cannot be tested without a real database or HTTP call | `04-dependencies/` |
| Which failures a function can produce is not visible in its signature | `05-error-handling/` |
| Retry and timeout logic is written ad hoc at each call site | `07-scheduling-and-retries/` |
| Adding tracing or metrics means threading them through every function | `09-observability/` |
| The same logic has to run on the server and in the browser | `10-platforms-and-hosting/` |
| You want to serve a typed HTTP contract | `11-http/` |

## Site structure: three areas

Guides and reference are organised on orthogonal axes — guides by reader task, reference by code structure —
so they cannot be interleaved. Separate top-level areas, as in Effect (Docs + API Reference), Rust (the Book
+ docs.rs), and Django (topics + reference).

**Top nav: Docs · Reference · Packages · GitHub.**

- **Docs** — the task folders above.
- **Reference** — the generated entity tree, enriched per entity by hand-written prose.
- **Packages** — install matrix and dependency graph. Separate from Reference because "what do I install" is
  asked far more often than "what is the signature of X". The two hosting adapters are the only packages
  carrying a dependency outside this project, and the matrix should say so plainly.

## FsLiveDocs prerequisites

Axial would use it as an ordinary consumer. Items 1–4 are needed before the reorganisation; 5–6 before the
reference can be honest about packaging.

1. **Preserve folder structure in output paths.** The real prerequisite. `ContentProvider.fs:272` flattens
   every page — `Path.GetFileNameWithoutExtension(f).ToLowerInvariant() + ".html"` — so files discovered
   recursively via `SearchOption.AllDirectories` collapse to the site root. `docs/guides/foo.md` becomes
   `/foo.html`, and same-named files in different folders collide silently. `collectGuideOutputs` (:148)
   flattens identically, and `validateLinks` builds its allowed-set from those names, so link validation
   changes with this.
2. **Folder-derived sections.** `View.fs:66-90` hardcodes a `guides` mapping for section id, display name,
   and order. Derive from folder name instead.
3. **Numeric prefix stripping** for ordering, in URLs and titles.
4. **Optional `_index.md` title override** per folder, for irregular casing ("JSON", "HTTP", "F#").
5. **Package identity in the model.** `PackageModel` is `{ Version; Entities; Scenarios }` — no package
   name — and `SymbolLister.merge` flattens N packages into one entity list, rebuilding the tree from
   namespace ids alone. With a dozen packages sharing the `Axial.*` prefix, the reference cannot tell a
   reader which NuGet a type ships in. Carry a package name through the merge and show it on every page.
6. **Areas as top nav, derived rather than hardcoded.** `View.fs:63-90` fixes `overview` / `guides` /
   `api-docs` with labels and ordering; derive them and render in the top bar.

**Deep reference is authored, not just generated.** `ContentProvider.applyApiDocs` reads
`docs/api/{EntityId}.md` and substitutes it for that entity's generated summary:

```fsharp
let summary = docs |> Map.tryFind e.Id |> Option.defaultValue e.SummaryHtml
```

So any namespace, module, or type can carry a full authored page keyed by its entity id. With `<example>`
blocks verified against the real assembly and snippet transclusion, reference depth lives next to the code
and cannot drift.

## Sequencing

| Phase | Work |
| --- | --- |
| 1 | FsLiveDocs items 1–4 |
| 2 | Collapse `docs/flow/` to `docs/`; migrate to FsLiveDocs; stop committing generated reference |
| 3 | Reorganise into the task folders above |
| 4 | Rewrite getting-started and the landing page |
| 5 | FsLiveDocs items 5–6, and the Packages area |

Phase 3 touches nearly every docs file and should not run concurrently with other docs work.

## Also outstanding

- **Dead cross-links.** `[text]({{< relref … >}})` renders as plain text with no anchor. The `{{% … %}}`
  form, absolute links, relative links, and `relref` inside a raw HTML `href` all work. Mechanical
  substitution, but likely moot if the FsLiveDocs migration lands first. Recount against `docs/flow/` — the
  original count spanned both products.
- **Move meta pages** (`packages-and-platforms.md`, benchmarks, AOT/trimming/Fable notes, comparisons) out
  of the learning path into `13-notes/`.
- **`overview.md` and `applications.md`** predate the split and should be folded into the landing page and
  `10-platforms-and-hosting/` respectively rather than surviving as loose top-level pages.
