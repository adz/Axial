# Repository Instructions

This file is for agent instructions, not user-facing documentation.

Keep a strict split between:

- agent instructions for contributors and coding agents
- user-facing documentation for library users

Do not put agent guidance in `README.md` or under `docs/`.

When writing or editing user-facing docs, follow the documentation guide in [`dev-docs/DOCS.md`](dev-docs/DOCS.md).

Before broad repository search, read [`dev-docs/AGENT_INDEX.md`](dev-docs/AGENT_INDEX.md) for the compact maintainer
map, generated-path rules, and task routing.

Refer to [`dev-docs/PLAN.md`](dev-docs/PLAN.md) for architectural direction and
[`dev-docs/TASKS.md`](dev-docs/TASKS.md) for the active queue.

## Architecture Invariants

- `Flow<'env, 'error, 'value>` is the public workflow model. Do not reintroduce public `Effect`, `EffectFlow`, `AsyncFlow`, `TaskFlow`, or carrier-specific workflow concepts.
- Keep `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, and `Axial.Validation` as independent leaf packages. `Axial.Flow` must not depend on `Axial.ErrorHandling`, `Axial.Refined`, or `Axial.Validation`.
- Model application and operational dependencies explicitly in `'env`; keep the ambient runtime for executor mechanics only.
- `Check` and `Result` live in `Axial.ErrorHandling`, `Parse`/`Refine`/`refine { }` live in `Axial.Refined`, `Validation`/`Diagnostics` live in `Axial.Validation`, and `BindError`/`Policy` live in `Axial.Flow`.
- Keep `Check` as a complete typed value-constraint subsystem: `Check<'value> = 'value -> Result<unit, CheckFailure list>`. Checks are path-free, raw-input-free value programs; value-preserving guards and extraction helpers belong in `Result`, and parsing and refined value construction belong in `Axial.Refined`.
- Use `BindError` only at a `flow { }` bind site when a source error must be assigned or mapped immediately before binding.
- Prefer AOT- and trimming-safe designs. Do not introduce runtime reflection as the foundation for core workflow, validation, schema, or service-access APIs; use explicit definitions first and consider build-time generation only after the API shape stabilizes.

## Dev Doc Organization

- Keep active architecture in `dev-docs/PLAN.md`, active work in `dev-docs/TASKS.md`, and high-level durable
  decisions in `dev-docs/decisions/README.md`.
- Keep completed work out of `dev-docs/TASKS.md`; keep the remaining active queue there for loop scripts.
- Keep speculative or pre-idea work in `dev-docs/current-ideas/`.
- Do not retain detailed historical specs after their useful decisions have been folded into current instructions. Delete stale specs instead of archiving large files that no longer match the codebase.

## Test Authoring

- Tests that demonstrate public APIs should use the expected end-user pipeline form, not a lower-level or transitional shape, unless the test is explicitly covering that lower-level API. Public API tests are examples readers copy from; keep their formatting aligned with the authoring style the library intends to teach.
- Do not define shared fixtures as module-level `let` values in xUnit test modules (schemas, refs, prebuilt inputs). Module-level bindings in test modules can be observed as null before file-level initialization runs, which surfaces as confusing `NullReferenceException`/`ArgumentNullException` failures. Build fixtures inside each test or expose them as functions (`let private mySchema () = ...`).

## Doc Workflow

- Treat `docs/reference/**`, `docs/examples/README.md`, `llms.txt`, and versioned docs as generated outputs or generator-backed outputs.
- When changing an API, update the source comments and the doc generator inputs first, then regenerate the docs. Do not hand-edit generated reference pages as the primary fix.
- When a user-facing guide needs to cite a new or renamed API, update the source comments and reference pages in the same pass, then run the generators immediately.
- For small checkbox tasks, regenerate directly affected docs as needed but defer `bash scripts/validate-docs.sh` until the phase end or a release/deploy checkpoint. `dev-docs/**` idea/planning notes do not require validation. For release/deploy checks, also run `npm run build` in `site`.

## Versioning and Compatibility

- **Before 1.0:** Bravely iterate. Remove old APIs and "old ways" immediately when a better alternative is established. Do not maintain compatibility aliases or stale patterns.
- **Post 1.0:** Standard semantic versioning applies. Maintain compatibility and use deprecation cycles for breaking changes.
- Pre-1.0 releases use one coordinated package version across `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Validation`, the umbrella `Axial` package, and the `Axial.Flow.*` add-on packages.
- The shared package version lives in `Directory.Build.props`; individual packable projects should not declare their own `<Version>`.
- A release tag such as `v0.7.0` produces all public Axial NuGet packages at version `0.7.0`.
- Revisit independent package versioning after the package boundaries stabilize, likely at or after 1.0.

## Documentation Integrity

- **Validate At Phase Or Release Boundaries:** For small checkbox tasks, defer `bash scripts/validate-docs.sh` until phase end or a release/deploy checkpoint, even after changes to user-facing docs, generated docs, public API signatures, XML comments, doc generator inputs, docs examples, reference docs, `llms.txt`, or site content. Regenerate affected generated docs during the task. `dev-docs/**` idea/planning notes and code-only changes with no public-doc impact do not require validation. Use `bash scripts/preview-docs.sh` only when a live server is needed for browser review or screenshots.
- **Preview Lifecycle:** `bash scripts/preview-docs.sh` stops cleanly on `SIGHUP`, `TERM`, or `INT`. It can also be stopped by creating `$AXIAL_DOCS_PREVIEW_STOP_FILE`, which defaults to `/tmp/axial-docs-preview.stop`.
- **Link Integrity:** Ensure that all cross-references between guides and reference pages are valid. Broken links degrade the experience for both humans and AI agents.
- **Code Highlighting:** Ensure all code examples are wrapped in triple-backticks with the `fsharp` language hint for proper syntax highlighting.
