# Axial Agent Index

Open this file after `AGENTS.md` and before broad repository search.

## Current Slice

The active queue is `dev-docs/TASKS.md`. Keep completed work out of that file, but keep the remaining active queue
there because loop scripts consume it directly.

Current product direction is in `dev-docs/PLAN.md`. Durable high-level decisions are in
`dev-docs/decisions/README.md`. Speculative sketches are in `dev-docs/current-ideas/` and should be opened only when the
task is about promoting, rejecting, or implementing that sketch.

## Package Graph

- `Axial.Flow`: independent workflow package (`src/Axial.Flow/`). Must not depend on `Axial.ErrorHandling` or
  `Axial.Schema`.
- `Axial.ErrorHandling` (`src/Axial.ErrorHandling/`): three namespaces in one package — `Axial.ErrorHandling`
  (`Check`, `CheckFailure`, `Result`, `Collection`, `result { }`), `Axial.Validation` (`Validation`, `Diagnostics`,
  paths, `validate { }`), and `Axial.Refined` (`Parse`, `Refine`, `refine { }`). Independent leaf — no dependency on
  `Axial.Schema` or `Axial.Flow`.
- `Axial.Schema` (`src/Axial.Schema/`): schema declaration (`Schema` module) and interpreters (`Model.parse`/
  `.reconstruct`, `Rules`, `Inspect`, `JsonSchema`, `RefinedSchema`) in one package. Depends on
  `Axial.ErrorHandling`.
- `Axial.Codec` (`src/Axial.Codec/`): compiled JSON codecs. Depends on `Axial.Schema`.
- `Axial.Flow.*` add-on packages depend on `Axial.Flow`.
- `Axial` umbrella package references the public packages for convenience.

## Open These First

- Flow/runtime/layers/services: `src/Axial.Flow/**`, relevant `src/Axial.Flow.*/*`, `tests/Axial.Flow.Tests/*Workflow*`,
  `tests/Axial.Flow.PlatformService.Tests/**`, and `dev-docs/PLAN.md`.
- Check/Result: `src/Axial.ErrorHandling/Check.fs`, `src/Axial.ErrorHandling/Result.fs`,
  `tests/Axial.ErrorHandling.Tests/CheckResultTests.fs`, `tests/Axial.ApiShape.Tests/ApiShapeTests.fs`, and `dev-docs/PLAN.md`.
- Refined values: `src/Axial.ErrorHandling/Refine.fs`, `src/Axial.ErrorHandling/Parse.fs`,
  `tests/Axial.ErrorHandling.Tests/{ParseAndBuilderTests,CatalogTests}.fs`, and the Check/Result files only as needed.
- Validation/diagnostics: `src/Axial.ErrorHandling/{Validation,Diagnostics,ValidateBuilder}.fs`,
  `tests/Axial.ErrorHandling.Tests/ValidationTests.fs`, and `dev-docs/PLAN.md`.
- Schema metadata/builder: `src/Axial.Schema/Schema.fs`, `tests/Axial.Schema.Tests/Schema*Tests.fs`, and the schema section in
  `dev-docs/PLAN.md`.
- Schema input/rules/interpreters: `src/Axial.Schema/{Model,RawInput,SchemaValidation,ParsedInput,Rules}.fs` and
  `tests/Axial.Schema.Tests/*ParseTests.fs`.
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
