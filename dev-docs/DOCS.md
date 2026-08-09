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
- `output/**`, `.livedocs/history/**`, and build artifacts are generated and untracked.

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

## Commands

```bash
bash scripts/build-docs-site.sh
bash scripts/validate-docs.sh
bash scripts/preview-docs.sh
```

The preview binds to `0.0.0.0:5000` so it is reachable from another device on the local network. Override either
value when needed:

```bash
AXIAL_DOCS_PREVIEW_HOST=127.0.0.1 AXIAL_DOCS_PREVIEW_PORT=8080 bash scripts/preview-docs.sh
```

Set `FSLIVEDOCS_ROOT` when FsLiveDocs is not checked out at `../../FsLiveDocs/main` relative to this repository.
Run full validation at phase and release boundaries.
