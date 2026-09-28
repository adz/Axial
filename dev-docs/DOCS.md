# Documentation guide

Axial has one user-facing documentation tree under `docs/`. Root `llms.txt` and `docs/llms.txt` are hand-written
machine entry points.

Write guides for library users under the numbered task folders. Keep contributor instructions under `dev-docs/` or
`AGENTS.md`, never in user documentation.

## Sources and generated output

- Public API facts originate in source XML comments.
- FsLiveDocs extracts API reference data from the built public projects.
- `docs/api/{EntityId}.md` can add an authored introduction to a generated entity page.
- FsLiveDocs copies non-Markdown files below `docs/` into the generated site, preserving their paths.
- `output/**`, `.livedocs/cache/**`, `.livedocs/releases/**`, and build artifacts are generated and untracked.
- `.livedocs/history.json` is the committed index of immutable release capsules.

Do not commit generated API pages. Update source comments or `docs/api/` enrichment pages, then rebuild.

## Authoring

- A top-level folder is a sidebar section. Prefix it with two digits to order it; FsLiveDocs strips the prefix from
  the generated URL.
- Prefix pages and nested folders with two digits at every depth. Pages and folders share one ordering, so their
  prefixes determine exactly where each appears in the sidebar. Do not use frontmatter weights.
- Use `_index.md` for a section landing page and to supply casing such as `HTTP` or `F#`.
- Link to guide output paths with `.html`. Use `xref:` for API entities and members.
- Use `{{< snippet id="..." >}}` for source snippets and `{{< example id="..." >}}` for extracted examples.
- Name APIs and behavior directly. Prefer short executable examples to restating signatures.
- Fence F# examples with `fsharp`.

## Examples that run

Every example should run and show or check its result. Prefer, in order:

- A `fsharp transcript` for a short example. Each transcript is its own FSI session: it sees the project's namespace
  but not the repository prelude or other blocks on the page, so open what it needs (`> open System;;`). Evaluated
  expressions print `val it: ...`; `printfn` output is not captured. A `let` bound to a value echoes
  `val name: ...`, often with an unstable rendering (`Flow <fun:ok@514>`), so bind functions (`let name () = ...`) or
  evaluate expressions directly. Type definitions do not echo. FSI prints tuples as `Tuple<string,int>` and
  `byte array` as `Byte[]`; copy expected output from a failing run rather than guessing.
- Compiled `fsharp` blocks for longer code, split into small functions, followed by a `fsharp run` block that checks
  the result with `shouldEqual expected actual`. A run block executes after the page's earlier `prepare`, ordinary,
  and `run` blocks, so a failing run also fails every later run on the page.
- `no-check` only with a specific reason: Fable-only code, packages the docs build does not reference (the
  OpenTelemetry SDK, Microsoft.Extensions.Hosting), code shown because it does not compile, or excerpts transcluded
  from a project that compiles and tests them (`{{< snippet ... mode="no-check" >}}`).

FsLiveDocs compiles `run` blocks with the repository prelude but executes them without it. Each page with a `run`
block therefore starts with a `fsharp prepare` block holding the prelude's opens and `shouldEqual`.

## Commands

```bash
dotnet build Axial.slnx --nologo -v minimal
dotnet livedocs test --warn-as-error
dotnet livedocs build --warn-as-error
dotnet livedocs watch
```

The preview binds to `0.0.0.0:5000` so it is reachable from another device on the local network. Override either
value when needed:

```bash
dotnet livedocs watch --host 127.0.0.1 --port 8080
```

The preview rebuilds when a watched `.fs`, `.fsproj`, `.fsx`, `.md`, or `.css` file changes. Generated and vendored
directories such as `.git`, `node_modules`, `artifacts`, `bin`, `obj`, and `output` are never watched. Exclude more
top-level directories with a comma-separated list:

```bash
dotnet livedocs watch --ignore examples,benchmarks
```

Run `bash scripts/check-docs-conventions.sh` and `dotnet livedocs test --warn-as-error` at phase and release boundaries.
