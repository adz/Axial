# Axial Agent Index

Open this file after `AGENTS.md` and before broad repository search.

## Current Slice

The active queue is `dev-docs/TASKS.md`. Keep completed work out of that file, but keep the remaining active queue
there because loop scripts consume it directly.

Current product direction is in `dev-docs/PLAN.md`. Durable high-level decisions are in
`dev-docs/decisions/README.md`. Speculative sketches are in `dev-docs/current-ideas/` and should be opened only when the
task is about promoting, rejecting, or implementing that sketch.

## Package Graph

- `Axial.Flow`: independent workflow package. Must not depend on `Axial.ErrorHandling`, `Axial.Refined`,
  `Axial.Validation`, or `Axial.Schema`.
- `Axial.ErrorHandling`: `Check`, `CheckFailure`, `Result`, `Collection`, and `result { }`. Independent leaf.
- `Axial.Refined`: parsing/refinement over `Axial.ErrorHandling`; no dependency on `Axial.Schema` or
  `Axial.Validation`.
- `Axial.Validation`: `Validation`, `Diagnostics`, paths, and `validate { }`. Independent leaf.
- `Axial.Schema`: schema/value-schema definitions and metadata. Independent of validation, refined values, and flow.
- `Axial.Validation.Schema`: schema interpreters; depends on schema, validation, error handling, and refined packages.
- `Axial.Flow.*` add-on packages depend on `Axial.Flow`.
- `Axial` umbrella package references the public packages for convenience.

## Open These First

- Flow/runtime/layers/services: `src/Axial.Flow/**`, relevant `src/Axial.Flow.*/*`, `tests/Axial.Flow.Tests/*Workflow*`,
  `tests/Axial.Flow.PlatformService.Tests/**`, and `dev-docs/PLAN.md`.
- Check/Result: `src/Axial.ErrorHandling/Check.fs`, `src/Axial.ErrorHandling/Result.fs`,
  `tests/Axial.ErrorHandling.Tests/CheckResultTests.fs`, `tests/Axial.ApiShape.Tests/ApiShapeTests.fs`, and `dev-docs/PLAN.md`.
- Refined values: `src/Axial.Refined/**`, `tests/Axial.Refined.Tests/**`, and the Check/Result files only as needed.
- Validation/diagnostics: `src/Axial.Validation/**`, `tests/Axial.Validation.Tests/ValidationTests.fs`, and
  `dev-docs/PLAN.md`.
- Schema metadata/builder: `src/Axial.Schema/Schema.fs`, `tests/Axial.Schema.Tests/Schema*Tests.fs`, and the schema section in
  `dev-docs/PLAN.md`.
- Schema input/rules/interpreters: start with `src/Axial.Validation.Schema/**`,
  `src/Axial.Schema/Schema.fs`, and the active queue.
- User-facing docs: read `dev-docs/DOCS.md` before editing `docs/**`, source comments, generated reference pages,
  `llms.txt`, or site content.
- Agent process/docs: `AGENTS.md`, this file, `dev-docs/TASKS.md`, and `dev-docs/PLAN.md`.

## Generated Or Noisy Paths

Default `rg` ignores generated/vendor-heavy paths through `.rgignore`:

- `docs/reference/**`
- `site/content/reference/**`
- `site/_vendor/**`
- `site/public/**`
- `BenchmarkDotNet.Artifacts/**`
- `.fsdocs/**`
- `output/**`

Search these with `rg -u` or an explicit target only when the task is about generated output, reference docs, site
artifacts, or build artifacts.

## Validation Commands

- Source/package moves: `bash scripts/check-source-inventory.sh`.
- Focused .NET tests: `dotnet test <project> --nologo -v minimal`.
- Public API/doc generator impact: update source comments or generator inputs first, regenerate affected docs, and defer
  `bash scripts/validate-docs.sh` until a phase or release checkpoint unless the task asks for full validation.
- Release/deploy doc checkpoint: `bash scripts/validate-docs.sh`, then `npm run build` in `site`.
- Live docs preview only when browser review is needed: `bash scripts/preview-docs.sh`.
