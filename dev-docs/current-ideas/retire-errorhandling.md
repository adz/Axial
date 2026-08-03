# Retire Axial.ErrorHandling; promote Result to a top-level product

**Status:** Phase 1 (expand Result) is **implemented**. The *package* half of Phase 4 is **implemented** as
project-split Phase 1B(a): `Axial.ErrorHandling` is deleted, and — going beyond what this document originally
proposed — so is the `Axial` umbrella, since `project-split.md` requires both gone before Flow is extracted. Phases
2-3 (the `docs/error-handling/` tree move) and the docs half of Phase 4 are **not started**; the docs tree still
lives at `docs/error-handling/`. The *presentation* half of Phases 2-3 landed early, because deleting the
meta-package while the landing page still advertised it would have been incoherent: the landing page now has two
peer doors (Result, Values), the sidebar has two `kind: primary` groups instead of one, and `_index.md`, `agent.md`,
and `llms.txt` state that Result is a product and Values is navigation only. What remains is the move itself — the
URL prefixes, the file relocation, splitting the shared prose pages, and the hardcoded product-area lists.

**Compatibility:** pre-1.0; no compatibility shim is required for the removed meta-package.

## Thesis

`Axial.ErrorHandling` is a dependency-only meta-package with no API. It exists only as an install
convenience, but it costs a documentation area, a sidebar `kind: primary` group, a URL prefix on four
unrelated packages, a validation script, and a landing-page door. The bundle it names is not a concept
adopters reason about: `Result` is about failure composition, while `Constraint`, `Refined`, and `Parse`
are about admitting values.

Target end state:

- `Axial.Result` becomes a top-level product entry point next to `Axial.Data`, `Axial.Schema`, and
  `Axial.Flow`, with its own docs tree at `/result`.
- `Constraint`, `Refined`, and `Parse` collapse under a single nav group titled **Values**.
  Values is a *navigation* grouping only — there is no `Axial.Values` package, no namespace, and no
  meta-package. The packages stay independent leaves and install individually.
- `Axial.ErrorHandling` is deleted: project, package id, solution entry, pack entry, docs-build entry,
  umbrella reference, and API-shape assertions.

## Package graph after the change

Unchanged code-wise. Only the meta-package edge disappears:

- `Axial.Result` — independent leaf.
- `Axial.Constraint` — independent leaf.
- `Axial.Parse` — independent leaf.
- `Axial.Refined` — depends on `Axial.Constraint`.

The `Axial` umbrella was removed in the same change rather than repointed at the four leaves, so there is no
meta-package left at any tier. Adopters who used `Axial.ErrorHandling` or `Axial` install the leaves they
actually use.

## Phases

### Phase 1 — Expand Result (current)

Do this before any move, so the promoted product has enough surface to justify a top-level door.

The existing package is already a solid fail-fast Result library. It is 281 lines across `Result.fs`,
`ResultBuilder.fs`, `Collection.fs`, and `Builders.fs`, and it already covers:

- `map`, `mapError`, `bind`, `orElse`, `orElseWith`;
- `requireTrue`, `okIf`, `failIf`, `guard`, `orError`, `okOr`, `errorOr`;
- `fromTry`, `fromChoice`, option/voption/nullable/null conversions, `defaultValue`, `headOr`;
- `Collection.traverseResult` and `Collection.sequenceResult` (fail-fast);
- a CE with `Return`, `ReturnFrom`, `Zero`, `Bind`, `Delay`, `Run`, `Combine`, `TryWith`, `TryFinally`,
  `Using`, `While`, and `For`.

So the expansion is small. **Applicative accumulation is the one addition that substantially changes what
the package can express**; everything else is ergonomics. Priority order:

1. **Accumulating `result.list` with `and!`.** `ResultBuilder` has no `MergeSources`, so `and!` does not
   compile today. Add an accumulating builder reached as a member off the existing instance:

   ```fsharp
   result.list {
       let! name = parseName input.Name
       and! age  = parseAge input.Age
       return name, age
   }
   ```

   The builder name selects the error container, and the container shows up in the result type:

   ```fsharp
   result.list  { … } : Result<'a, 'e list>
   result.array { … } : Result<'a, 'e[]>
   ```

2. `result.array` — same semantics, different container.
3. **Surface `traverse`/`sequence` naturally.** The namespace is already `Axial.Result`, so
   `Collection.traverseResult`/`sequenceResult` stutter. Rename to `Result.traverse`/`Result.sequence`.
   Make the seq-in/list-out shape explicit in the docs rather than leaving it implied by the signature.
   Accumulating counterparts (`traverseAll`/`sequenceAll`) fit naturally afterwards, but the
   container-specific CEs may make them unnecessary — do not add them speculatively.
4. `tap` / `tapError` — synchronous, tiny, no new semantics, useful at boundaries and while debugging:

   ```fsharp
   Result.tap      : ('a -> unit) -> Result<'a, 'e> -> Result<'a, 'e>
   Result.tapError : ('e -> unit) -> Result<'a, 'e> -> Result<'a, 'e>
   ```

5. `BindReturn` — lets `let! v = op () in return f v` compile without the avoidable intermediate bind
   shape. Pure optimisation, no surface change.
6. Error-side bind that can change the error type. `mapError` and `orElseWith` cover pure mapping and
   same-error-type recovery; neither covers a recovery step that itself fails with a different error:

   ```fsharp
   ('e1 -> Result<'a, 'e2>) -> Result<'a, 'e1> -> Result<'a, 'e2>
   ```

   Name it `recoverWith`, not `bindError` — it is not a lawful symmetric bind in the bifunctor sense, and
   `recoverWith` states the intent. Lowest priority; ship only if a real call site wants it.

#### Decisions

- **The container is part of the builder's type, and the named builders are separate types.** Each named
  builder stays generic in `'error` and pins the container syntactically:
  `member _.Source(r: Result<'a,'e>) : Result<'a, 'e list>` for `result.list`, and so on. F# has no
  higher-kinded types, so a single builder type cannot be parameterised over "container of `'e`". They
  share an `internal Accumulate.mergeSources` core taking `append` as an argument; the sharing is at the
  implementation level, not the type level.
- **`result.withCollection` is not shipped.** It was proposed as the general form the named builders
  specialise, but the absence of higher-kinded types means the named builders cannot be derived from it —
  so it earns nothing structurally and is a fourth surface to maintain for a genuine edge case. It also
  forced the worst part of the design: the extension `Source` could not reach the constructor arguments,
  needing an extra public `Lift` member purely as a workaround. Anyone wanting another container maps off
  `result.list` with `Result.mapError`.
- **`let!`/`and!` semantics are mixed, and the compiler already decides this.** F# desugars an `and!`
  group through `MergeSources` and a subsequent `let!` through `Bind`: accumulate within an `and!` group,
  fail fast between groups. Applicative-only would mean deliberately omitting `Bind` so sequential
  binding fails to compile, which is strictly worse. This is a documentation obligation, not a design
  choice — the docs must show the exact boundary, because "why didn't it collect both errors" is the
  question adopters will ask.
- **`Source` is required on the accumulating builders, and excluded everywhere else.** The canonical type
  is `Result<'a, 'e list>` but the bindings are ordinary `Result<'a, 'e>`, so a
  `Source : Result<'a,'e> -> Result<'a, 'e list>` lift is what makes the builder usable — without it
  every binding needs a hand-written `Result.mapError List.singleton`, which removes the ergonomic reason
  to have the feature. This is not a hidden conversion of a foreign type. Plain `result { }` gets no
  `Source`, and no builder gets `Option`, `Nullable`, or `voption` overloads: those would undermine the
  explicit `someOr`/`nullableOr`/`valueSomeOr` helpers the package already designed.
- **`result.set` is dropped from the initial cut.** It is the only builder that constrains `'error` (it
  needs `comparison`), and silently deduplicating failures is a surprising default. `Result.mapError
  Set.ofList` off `result.list` covers it for anyone who wants it.
- **`result.seq` is not shipped.** Laziness would buy nothing: accumulation only runs when both sides are
  already `Error`, so both error values are fully evaluated and `Seq.append` would defer concatenating
  values that already exist. What settles it is the type rather than the strategy — `seq<'error>` has no
  structural equality, so `Error [...] = Error [...]` compares by reference and fails confusingly in
  tests and in any code comparing error values. `Result.mapError Seq.ofList` off `result.list` covers
  anyone who needs the signature.
- **The two `Source` overloads are split intrinsic/extension.** The identity overload (already-collected
  result passes through) is intrinsic on each builder; the lifting overload is an extension member in an
  `[<AutoOpen>]` module. The two overlap for `Result<'a, 'e list>`, and F# prefers intrinsic over
  extension, which resolves it deterministically. Declaring both as intrinsic members makes every binding
  ambiguous — this was a real compile failure during implementation, not a hypothetical.

#### Constraints

- `Axial.Result` must remain an independent leaf. Nothing added here may reference `Axial.Constraint`,
  `Axial.Refined`, or `Axial.Data`. Any violation preset belongs in `Axial.Constraint`.
- Path-aware accumulation stays `Axial.Schema`'s. These builders accumulate a flat container of errors
  with no path, no field identity, and no reconstruction.
- Each addition needs an XML doc comment with an example, tests in `tests/Axial.Result.Tests`, and a
  regenerated reference page.

#### What shipped

`src/Axial.Result/Collection.fs` is deleted; `Accumulate.fs` holds the two accumulating builders and the
shared merge core. `Result.traverse`/`Result.sequence`/`Result.tap`/`Result.tapError` are on the `Result`
module; `ResultBuilder` gained `BindReturn` and the `list`/`array` members. `recoverWith` (item 6)
was not implemented — no call site wanted it. Reference pages regenerate correctly after updating the
`Axial.Result.Collection.*` symbol ids in `scripts/docgen/Program.fs`; leaving them stale made the
generator fuzzy-match Refined's `traverseResult` into the Result reference.

#### Extra impact from the rename in (3)

Renaming `Collection.traverseResult`/`sequenceResult` to `Result.traverse`/`sequence` retires the
`Collection` module. That moves `docs/error-handling/reference/result/collection/m-result-collection-*.md`
into the `result` reference group, changes the sidebar `children_of` shape, and touches
`dev-docs/API_BASELINE.md` and any call sites in `src/`, `tests/`, `examples/`, and `benchmarks/`. Doing
this rename in Phase 1 — before the docs move — avoids relocating pages that are about to be deleted.

### Phase 2 — Move Result docs to `/result`

Follow the precedent set by `ce58acad` ("Promote Data to an independent docs entry point").

- `docs/error-handling/result/**` → `docs/result/**`. The section is already standalone and self-contained
  (landing page plus eight task pages), so this is a move with no rewriting. `docs/error-handling/tutorials/**`
  stays with Constraint — it is a Constraint tutorial that happens to use Result.
- `docs/error-handling/reference/result/**` → `docs/result/reference/**`.
- Add `docs/result/_index.md` (product landing, `menu.main` weight), `docs/result/agent.md`, and
  `docs/result/llms.txt`.

### Phase 3 — Collapse the rest under "Values"

- `docs/error-handling/{check,refined,parse}/**` → `docs/values/{check,refined,parse}/**`, and
  `docs/error-handling/reference/{check,predicate,refined,parse}/**` → `docs/values/reference/…`.
- One `site/data/sidebars/values.yaml` with a `kind: primary` **Values** group and per-package subgroups
  for Constraint, Refined, and Parse. Each subgroup caption states that the package installs
  independently.
- Keep `fstoolkit-comparison.md` with Result. `getting-started.md`, `overview.md`, and `reference-app.md`
  cover the whole former bundle — split them: Result-composition material to `/result`, value-admission
  material to `/values`.

### Phase 4 — Delete the package and the old tree

Delete `src/Axial.ErrorHandling/`, `docs/error-handling/`, `site/data/sidebars/error-handling.yaml`, and
`scripts/validate-error-handling-docs.sh`.

## Impacts

### Source and build

| File | Change |
| --- | --- |
| `src/Axial.ErrorHandling/Axial.ErrorHandling.fsproj` | deleted |
| `src/Axial/` (umbrella project and `Builders.fs`) | deleted |
| `Axial.slnx:53` | remove project entry |
| `scripts/pack.sh:28` | remove from the pack list |
| `scripts/docs-build.proj:7` | remove `DocsProject` entry |
| `scripts/build-docs-site.sh`, `scripts/docs-build.proj` | the umbrella and meta entries built the leaves transitively; the four leaves are now listed explicitly |
| `src/Axial.{Result,Constraint,Parse}/*.fsproj` | drop `Axial.ErrorHandling` from `PackageTags`/description text where present |

### Tests

- **Done.** The assertions that the meta-package exists and exports no public type are replaced by
  `no meta-package remains in the graph`: no package assembly may reference `Axial` or `Axial.ErrorHandling`, and
  neither DLL may appear in the test output directory. Since the umbrella went too, there is no "umbrella
  references the four leaves" assertion to write.
- No behavioural test changes. Phase 1 adds tests to `tests/Axial.Result.Tests`.

### Docs pipeline

- `scripts/populate-hugo-content.sh` hardcodes four product areas (`data`, `error-handling`, `schema`,
  `flow`) in the copy, `llms.txt`, and `static/` steps, and does a `weight` upsert on
  `.../reference/error-handling/_index.md`. It becomes five areas: `data`, `result`, `values`, `schema`,
  `flow`.
- `scripts/validate-product-docs.sh` accepts `data|validation|schema|flow` and has a `validation` branch
  asserting `error-handling/getting-started`, `error-handling/diagnostics`,
  `error-handling/reference/check/t-errorhandling-check`, and sidebar-id uniqueness under
  `error-handling/reference/result`. Split into `result` and `values` branches with equivalent assertions.
- `scripts/validate-error-handling-docs.sh` → replaced by `validate-result-docs.sh` and
  `validate-values-docs.sh`.
- `scripts/generate-api-docs.mjs` / `scripts/docgen` — output paths for the moved reference trees.
- `site/assets/scss/_styles_project.scss` — `.axial-door--result` already exists; add or rename a
  `--values` door variant.
- `site/layouts/_partials/{sidebar,pager,page-meta-links}.html` — check for `error-handling` path
  assumptions.

### Prose to update

`docs/index.md` (the ErrorHandling door becomes two: Result and Values), `docs/error-handling/_index.md`
(the "Packages" table and the "`Axial.ErrorHandling` installs all four" line), `docs/result/_index.md`
(the "installs as part of `Axial.ErrorHandling`" installation line), `README.md`, `llms.txt`,
`RELEASE_NOTES.md`, `prd.md`, `examples/README.md`,
`examples/Axial.ReferenceApp.Intro/{README.md,Program.fs,*.fsproj}`,
`examples/Axial.Hosting.DotNet/*.fsproj`, `benchmarks/Axial.Flow.Benchmarks/*`, plus cross-links in
`docs/{schema,flow}/**`.

### dev-docs to update

- `AGENT_INDEX.md` — drop the `Axial.ErrorHandling` bullet from the package graph; state that Values is a
  docs grouping with no package behind it.
- `PLAN.md:10,27,33` — the 1.0 gate and umbrella description are stated in terms of the meta-package.
- `DOCS.md` — the product list, the source-of-truth list, and the validate-script list.
- `decisions/README.md` — add a decision entry recording the retirement and the docs-only Values
  grouping; the existing entries at lines 106-128, 152-173, and 359-369 describe the meta-package as
  current and need superseding notes.
- `API_BASELINE.md`, `TASKS.md`, `ReleaseProcess.md`, `current-ideas/{project-split,refined-parse-cleanup,architecture-guardrails}.md`
  — mechanical mentions.

### Release

The `Axial.ErrorHandling` package id is retired. Since this lands pre-1.0 and no released version is
depended on downstream, no deprecation push is planned; `RELEASE_NOTES.md` records the removal and the
"install the leaves or the umbrella" migration line.

## Risks

- **Discoverability.** ErrorHandling was the single door for four packages. Two doors plus a Values group
  must not read as "Result is unrelated to checking." The `/result` and `/values` landing pages each need
  a one-line cross-link stating how they compose (`Constraint` returns the standard `Result`; `Axial.Result`
  helpers work on it).
- **"Values" without a package.** Every nav caption and both landing pages must say the packages install
  independently, or adopters will search NuGet for `Axial.Values`.
- **Docs-pipeline breadth.** Five product areas is a hardcoded-list change in at least four scripts;
  Phases 2 and 3 should each end with a full `bash scripts/validate-docs.sh`.
- **Scope creep in Phase 1.** Expanding Result invites reimplementing FsToolkit. The filter stays: add an
  operation only when it removes a branch or a hand-rolled helper from consumer code — not when it is
  another spelling of an existing helper. Items 5-7 are all individually droppable; item 1 is not.
- **Accumulating builders are the surface most likely to need a breaking fix later.** They are also the
  reason to promote Result at all, so they should land and be exercised by the reference app before the
  Phase 2 docs move freezes the story.
