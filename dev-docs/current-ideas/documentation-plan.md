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

## Use FsLiveDocs as it exists

FsLiveDocs is not a drop-in replacement for the current Hugo pipeline yet, but it already owns more of the
pipeline than this sketch originally assumed:

- `livedocs build` accepts multiple project files, extracts API models from their built DLL and XML files,
  enriches entities from `docs/api/{EntityId}.md`, renders Markdown below `docs/`, writes `output/`, and runs
  Pagefind.
- `livedocs test` executes documented examples. `livedocs generate-tests` generates an xUnit/Verify project
  for examples marked as snapshots. The migration must choose one verification path and put that exact
  command in Axial's validation scripts; "FsLiveDocs verifies examples" is not itself a build step.
- `{{< snippet id="..." >}}`, `{{< example id="..." >}}`, and `xref:` are FsLiveDocs syntax. Existing Hugo
  `relref` shortcodes are not portable and must be converted during migration.
- The CLI currently assumes `docs/`, `.livedocs/`, and `output/` relative to its working directory and invokes
  `npx -y pagefind`. Axial should initially conform to that contract rather than add Axial-only path options.

### Generic guide-tree support now landed in FsLiveDocs

The reusable prerequisite for this information architecture has been implemented in FsLiveDocs:

1. Markdown folder structure is preserved in output paths and local-link validation. Output collisions fail
   the build instead of silently overwriting a page.
2. Leading numeric ordering prefixes are removed from folder and page URLs and from inferred page titles.
3. Sidebar sections are derived from the top-level folder. A section `_index.md` supplies its display title,
   including intentional casing such as "HTTP" or "F#".
4. Nested pages receive the correct root-relative navigation links.
5. An authored `docs/index.md` is the homepage; the renderer no longer overwrites it with the FsLiveDocs
   product landing page.

### Remaining general FsLiveDocs work

These are separate capabilities and should land as separate tool changes:

1. **Consumer identity and navigation.** The document body is consumer-owned now, but the HTML title,
   navbar brand, and top navigation still say FsLiveDocs and expose only Home and API. Extend `SiteConfig`
   with general site identity and navigation/area configuration. Do not add Axial-named branches in the
   renderer.
2. **Package provenance.** `PackageModel` is `{ Version; Entities; Scenarios }`; `SymbolLister.merge` merges
   projects by entity id and discards their package identity. Carry assembly/package identity through
   extraction and merge, then display it on entity pages. This is required before Axial's Reference and
   Packages areas can make accurate NuGet claims.
3. **Versioned guide links.** Confirm nested guide, xref, asset, and Pagefind links under
   `output/history/{version}/`. Version snapshots currently preserve API data, not a versioned copy of guide
   source, so the intended historical-guide semantics need an explicit decision and tests.

**Deep reference is authored, not just generated.** `ContentProvider.applyApiDocs` reads
`docs/api/{EntityId}.md` and substitutes it for that entity's generated summary:

```fsharp
let summary = docs |> Map.tryFind e.Id |> Option.defaultValue e.SummaryHtml
```

So any namespace, module, or type can carry a full authored introduction keyed by its entity id. Member
tables and signatures remain generated. `<example>` blocks and transcluded snippets reduce drift only when
Axial runs the corresponding FsLiveDocs test/generation commands in CI.

## Sequencing

| Phase | Work |
| --- | --- |
| 1 | Add general FsLiveDocs consumer identity/navigation configuration; prove a small Axial build against the local tool |
| 2 | Collapse `docs/flow/` to `docs/`; convert Hugo links; replace the Hugo/docgen scripts with explicit FsLiveDocs build and example-verification commands; stop committing generated reference |
| 3 | Reorganise into the task folders above using numeric prefixes and `_index.md` section metadata |
| 4 | Rewrite getting-started and the authored `docs/index.md` landing page |
| 5 | Add package provenance to FsLiveDocs, then build the Reference and Packages areas |

Phase 3 touches nearly every docs file and should not run concurrently with other docs work.

## Also outstanding

- **Dead cross-links.** Do not repair these by changing Hugo shortcode delimiters. Inventory them against
  `docs/flow/`, then convert them to relative Markdown links or FsLiveDocs `xref:` links as part of phase 2.
- **Move meta pages** (`packages-and-platforms.md`, benchmarks, AOT/trimming/Fable notes, comparisons) out
  of the learning path into `13-notes/`.
- **`overview.md` and `applications.md`** predate the split and should be folded into the landing page and
  `10-platforms-and-hosting/` respectively rather than surviving as loose top-level pages.
