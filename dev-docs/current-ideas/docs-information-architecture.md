# Two Projects: Axial And FsFlow — Split And Documentation Plan

Status: planned work. Supersedes `project-split.md`'s three-repository and docs-shaping sections.

Everything below is work to do. Reasoning that led here is not repeated.

---

## 1. The Decisions

1. **Two independent projects**, not a family with an umbrella.
   - **Axial** — constraints, values, schema. Keeps the name; in practice it already means this.
   - **FsFlow** — the effect system. Restores the identity last published at 0.6, which is the only
     released identity; nothing has ever shipped as `Axial.Flow`.
2. **No umbrella**: no third repository, no shared site, no mounts, no cross-repo `xref:`, no assembly
   pipeline. Each project has one repository and one site.
3. **The shared thesis is stated in prose, not encoded in package IDs.** Axial encodes invariants about
   values, boundaries, and models; FsFlow encodes invariants about computation — what can fail, what it
   requires, how it runs concurrently. One sentence on each site; no shared code.
4. **Navigation is by reader task, not by package**, in both projects.
5. **Generated reference is never committed** — it is produced at build.
6. **Nothing is published until the shape is settled.** No Axial package has ever been released, so package
   boundaries and names are free to change today and breaking tomorrow. That window closes at first
   publish.

---

## 2. Package Inventory And The Seam

Core dependency graph:

| Package | Depends on | Depended on by |
| --- | --- | --- |
| `Axial.Result` | — | nothing |
| `Axial.Parse` | — | Schema |
| `Axial.Constraint` | — | Refined, Schema |
| `Axial.Refined` | Constraint | Schema |
| `Axial.Data` | — | Schema |
| `Axial.Schema` | Constraint, Data, Parse, Refined | — |
| `Axial.Flow` | — | the two server adapters below |
| `Axial.Schema.Http` | Schema | the two server adapters below |
| `Axial.Schema.Http.AspNetCore` | **Flow**, Schema.Http, Schema.Json, Data | — |
| `Axial.Schema.Http.GenHttp` | **Flow**, Schema.Http, Schema.Json, Data | — |

The two cores are independent. **One real seam exists**: the HTTP server adapters depend on both, and Flow
is in their public API rather than an implementation detail —

```fsharp
let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model>
(workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IResult>)
```

So the integration is: **declare a contract with Schema, serve it with FsFlow.**

`Axial.Schema.Http` is Flow-free — 406 lines describing endpoints and emitting an OpenAPI document via
`OpenApi.document : OpenApiInfo -> EndpointSpec list -> string`. Contract-first use (emit `openapi.json`,
generate clients, run contract tests) needs no server, so that layer stays in Axial and stands alone.

**Ownership after the split.** Description must not depend on execution, so the glue goes with the runtime:

| Today | After |
| --- | --- |
| `Axial.Schema.Http` | stays in Axial, unchanged |
| `Axial.Schema.Http.AspNetCore` | → `FsFlow.Http.AspNetCore`, depends on `Axial.Schema.Http` |
| `Axial.Schema.Http.GenHttp` | → `FsFlow.Http.GenHttp`, depends on `Axial.Schema.Http` |

Axial then has zero knowledge of FsFlow. FsFlow carries two optional satellites that pull `Axial.Schema.Http`
only if used.

---

## 3. Work: Repository Split

### 3.1 Method

Use `git filter-repo` (not `filter-branch`, which is deprecated; not a fork, which carries every unrelated
file's history forever and is marked as a fork by GitHub). Not currently installed — `pip install
git-filter-repo`.

**Asymmetric, deliberately:**

- **FsFlow** — fresh `git clone --no-local` into scratch, `filter-repo` down to the Flow paths, push to a
  new empty repository. Clean history, `git blame` and `git bisect` intact over the 33 commits that touch
  `src/Axial.Flow`.
- **Axial** — an ordinary `git rm` commit removing the Flow paths. **Do not `filter-repo` the existing
  repository**: it rewrites every SHA and breaks existing clones and branches. Axial's history honestly
  includes Flow, and should keep saying so.

### 3.2 Paths to extract

```
src/Axial.Flow                      src/Axial.Flow.PlatformService
src/Axial.Flow.Console              src/Axial.Flow.Process
src/Axial.Flow.FileSystem           src/Axial.Flow.Telemetry
src/Axial.Flow.Hosting              src/Axial.Flow.Telemetry.JavaScript
src/Axial.Flow.Hosting.Browser      src/Axial.Flow.Telemetry.Shared
src/Axial.Flow.Hosting.Node
src/Axial.Flow.HttpClient           src/Axial.Schema.Http.AspNetCore   ← becomes FsFlow.Http.*
                                    src/Axial.Schema.Http.GenHttp

tests/Axial.Flow.Tests              tests/Axial.Flow.Integration.Tests
tests/Axial.Flow.Comparisons.Tests  tests/Axial.Flow.PlatformService.Tests
tests/Axial.Flow.FileSystem.Tests   tests/Axial.Flow.Telemetry.Tests
tests/Axial.Flow.Hosting.Tests
tests/Axial.Flow.HttpClient.Tests

docs/flow
benchmarks/Axial.Flow.Benchmarks
```

**Needs individual inspection before assignment** — these reference Flow but may exercise both sides:

- `examples/Axial.Hosting.Browser`, `.Desktop`, `.DotNet`, `.GenericHost`, `.Node`
- `examples/Axial.Examples`, `.App.Example`, `.MaintenanceExamples`, `.ReadmeExample`, `.ReferenceApp`,
  `.Playground`
- `benchmarks/Axial.Benchmarks.Fable`
- `tests/Axial.ApiShape.Tests`

Some will need one copy each side.

### 3.3 Order

1. Enumerate and verify the path list; confirm each ambiguous example's true dependencies.
2. Extract to FsFlow. **Do not rename in the same pass** — `--path-rename` would make history read as
   though it was always FsFlow. Extraction must be mechanically reviewable.
3. Confirm the extracted repository builds and its tests pass standalone.
4. Rename `Axial.Flow` → `FsFlow` in ordinary commits: package IDs, namespace, the two HTTP adapters.
5. Duplicate shared scaffolding into FsFlow: `Directory.Build.props`, `mise.toml`, CI workflows, test
   conventions, docs theme.
6. Only then remove the Flow paths from Axial.
7. Publish Axial first (FsFlow's adapters depend on `Axial.Schema.Http`), then FsFlow.

---

## 4. Work: Package Shape, Before First Publish

Free today, breaking after first publish.

1. **Merge `Axial.Constraint` into `Axial.Refined`.** Constraint alone is clunky — check, then map or
   render — and anyone going that far will take Schema too. Constraint + Refined is the real standalone
   unit: domain invariants with no boundary and no serialization. `Constraint` becomes a module inside
   `Refined`. Package boundaries do not control payload — trimming and tree-shaking work on reachability,
   not package identity — so nothing is lost by merging.
2. **Leave `Axial.Parse` alone.** Zero dependencies, a crisp standalone story, a self-explanatory name.
3. **Apply for NuGet prefix reservation**: `Axial.*` and `FsFlow*`.
4. **Request the `Flow` package ID** from its current owner (unlisted, .NET Standard 1.1, empty repo) if
   wanted — but do not wait on it or plan around it. Package ID and namespace are independent, and the
   namespace must stay qualified regardless, so winning it changes one line in a `.fsproj` and nothing a
   user types.

---

## 5. Work: Documentation, Per Project

Folder name is the section name, so the IA is expressed by naming folders after reader tasks. Numeric
prefixes order them and are stripped from URLs.

### 5.1 Axial — `./docs`

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

### 5.2 FsFlow — `./docs`

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
11-http/                     HttpClient, and serving Axial.Schema contracts
12-testing/                  fakes, layer swapping
13-notes/
```

Sections 6–9 are inferred from module names (`Schedule.fs`, `Policy.fs`, `Stm.fs`, `Stream.fs`, `Ref.fs`,
`Concurrency.fs`) rather than from what they teach. Open: whether `Schedule` and `Policy` are one topic or
two, whether `Stm` and `Ref` are public API worth teaching, whether `Stream` warrants its own section.

### 5.3 Getting started, one per project

**Axial** — declaration shaped: declare once, then derive.

1. The problem: the same rule restated in a parser, a validator, a form, and a test
2. Install what you need
3. Declare one constraint; check a value with it
4. Attach the declaration to a type so nothing re-checks it
5. Declare a whole model; derive the codec and contract from it
6. Derive test fixtures from the same declaration
7. How failures are reported: an ordinary F# `Result` carrying a `Violation` or `ParseError` — Axial's
   types, derived from the rule, carrying context worth keeping
8. Where to go next

**FsFlow** — root-type shaped, but problem-led rather than type-led. Effect opens with `Effect<A, E, R>` as
a type shape; the likeliest curious newcomer here is a C# developer who has never heard of ZIO or Effect,
and a three-parameter generic on page one will lose them.

1. The problem: a handler needs a database and can fail, and neither fact is in its signature
2. Install
3. Your first flow — write one, run it
4. Failure moves into the signature — the `'error` slot, contrasted with exceptions
5. Dependencies move into the signature — the `'env` slot, contrasted with constructor injection and DI
   containers
6. Putting it together with the `flow { }` builder
7. Swapping the dependency in a test — the payoff
8. Where to go next

### 5.4 Landing page per project: route by symptom

Plain problem statements the destination page genuinely solves. Claim only what the library does —
observability is the honest promise; diagnosing a slow production system is not.

**Axial**

| Problem | Goes to |
| --- | --- |
| Validation boilerplate is everywhere, and invalid values still get through | `03-validating-values/` |
| The same rule is repeated in a parser, a validator, a form, and a test | `06-modelling/` |
| Decoding and validation are separate steps that drift apart | `07-json/` |
| Client and server disagree about the shape of a request | `08-http-contracts/` |
| Constructing test data by hand is slow and repetitive | `09-testing/` |
| You want one small library, not a framework | Packages index |

**FsFlow**

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

## 6. Work: FsLiveDocs

Both projects use it as an ordinary consumer. Only four items are needed; mounts, artifact packaging, and
merged symbol tables are not, since there is no merged site.

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

Do these while the only consumer is FsLiveDocs' own small docs tree. Once Axial migrates, the same change
churns a large tree; doing it first means Axial migrates once, directly onto nested output.

---

## 7. Sequencing

| Phase | Work | Where |
| --- | --- | --- |
| 1 | FsLiveDocs items 1–4 | FsLiveDocs |
| 2 | Merge `Constraint` into `Refined` | Axial (combined repo) |
| 3 | Verify split path list; inspect ambiguous examples and tests | combined repo |
| 4 | `filter-repo` extract to FsFlow; confirm it builds green | new repo |
| 5 | Rename `Axial.Flow` → `FsFlow`; move the two HTTP adapters | FsFlow |
| 6 | Remove Flow paths from Axial | Axial |
| 7 | Migrate docs to FsLiveDocs; stop committing generated reference | both |
| 8 | Reorganise into task folders (§5.1, §5.2) | both |
| 9 | Getting-starteds, landing pages, the two lead pages (§5.5) | both |
| 10 | Prefix reservations; publish Axial, then FsFlow | both |

Phases 2–3 are cheapest in the combined repository. Phase 8 touches nearly every docs file and should not
run concurrently with other docs work.

---

## 8. Open Questions

1. **Is `Data` a Foundation or a Schema satellite?** It exists because building maps of lists by hand in
   tests and docs was miserable, which is a testing story. But it may be the easiest package to adopt
   first, which argues for prominence.
2. **Does a plain-ASP.NET serving path need a package?** After the split, Axial declares contracts and
   emits OpenAPI but ships no server. Write the "serve it on plain ASP.NET" page first (§5.1, folder 08);
   if the manual wiring turns out to be boilerplate people copy every time, it earns a package. If not, a
   page is the whole answer.
3. **Flow sections 6–9** — see §5.2.
