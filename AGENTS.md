# Repository Instructions

This file is for agent instructions, not user-facing documentation.

Keep a strict split between:

- agent instructions for contributors and coding agents
- user-facing documentation for library users

Do not put agent guidance in `README.md` or under `docs/`.

When writing or editing user-facing docs, follow the documentation guide in [`dev-docs/DOCS.md`](dev-docs/DOCS.md).

Refer to [`dev-docs/PLAN.md`](dev-docs/PLAN.md) for architectural direction and [`dev-docs/TASKS.md`](dev-docs/TASKS.md) for the active backlog.

## Doc Workflow

- Treat `docs/reference/**`, `docs/examples/README.md`, `llms.txt`, and versioned docs as generated outputs or generator-backed outputs.
- When changing an API, update the source comments and the doc generator inputs first, then regenerate the docs. Do not hand-edit generated reference pages as the primary fix.
- When a user-facing guide needs to cite a new or renamed API, update the source comments and reference pages in the same pass, then run the generators immediately.
- Do not consider any doc change done until `bash scripts/validate-docs.sh` passes. For release/deploy checks, also run `npm run build` in `site`.

## Versioning and Compatibility

- **Before 1.0:** Bravely iterate. Remove old APIs and "old ways" immediately when a better alternative is established. Do not maintain compatibility aliases or stale patterns.
- **Post 1.0:** Standard semantic versioning applies. Maintain compatibility and use deprecation cycles for breaking changes.
- `Axial.Flow`, `Axial.Result`, `Axial.Validation`, the umbrella `Axial` package, and the `Axial.Flow.*` add-on packages are versioned independently until a release policy says otherwise.
- **Experimental Status:** The `Axial.Flow.*` add-on packages (PlatformService, Console, FileSystem, HTTP, Process, Hosting, Telemetry, etc.) are currently considered experimental and are **not** included in the public NuGet release cycle. Their versioning relationship to the primary packages is TBD.
- Do not force the core release line to wait for the least mature service package.
- Treat `Axial.Flow.*` add-on packages as optional add-ons until each package has its own stable release story.

## Documentation Integrity

- **Always Validate:** Every change to the codebase or documentation must be followed by a documentation build via `bash scripts/validate-docs.sh` to ensure correct rendering. Use `bash scripts/preview-docs.sh` only when a live server is needed for browser review or screenshots.
- **Preview Lifecycle:** `bash scripts/preview-docs.sh` stops cleanly on `SIGHUP`, `TERM`, or `INT`. It can also be stopped by creating `$AXIAL_DOCS_PREVIEW_STOP_FILE`, which defaults to `/tmp/axial-docs-preview.stop`.
- **Link Integrity:** Ensure that all cross-references between guides and reference pages are valid. Broken links degrade the experience for both humans and AI agents.
- **Code Highlighting:** Ensure all code examples are wrapped in triple-backticks with the `fsharp` language hint for proper syntax highlighting.
