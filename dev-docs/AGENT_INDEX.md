# Axial Agent Index

Open this file after `AGENTS.md` and before broad repository search.

## Current Slice

The active queue is `dev-docs/TASKS.md`. Keep completed work out of that file, but keep the remaining active queue
there because loop scripts consume it directly.

Current product direction is in `dev-docs/PLAN.md`. Durable high-level decisions are in
`dev-docs/decisions/README.md`. Speculative sketches are in `dev-docs/current-ideas/` and should be opened only when the
task is about promoting, rejecting, or implementing that sketch.

Working on `src/Axial.Schema`? Read `dev-docs/schema/internals.md` first (implementation map), and
`dev-docs/schema/constructor-last.md` for the current authoring-surface direction.

## Package Graph

- `Axial.Flow`: independent workflow package (`src/Axial.Flow/`). Must not depend on `Axial.ErrorHandling` or
  `Axial.Schema`.
- `Axial.ErrorHandling` (`src/Axial.ErrorHandling/`): three namespaces in one package — `Axial.ErrorHandling`
  (`Check`, `CheckFailure`, `Result`, `Collection`, `result { }`), `Axial.Validation` (`Validation`, `Diagnostics`,
  paths, `validate { }`), and `Axial.Refined` (`Parse`, `Refine`, `refine { }`). Independent leaf — no dependency on
  `Axial.Schema` or `Axial.Flow`.
- `Axial.Schema` (`src/Axial.Schema/`): schema declaration (`Schema` module), parsing and checking (`Schema.parse`,
  `Schema.parseRetainingInput`, `Schema.check`), inspection (`Inspect`), contracts,
  and refined schema adapters (`RefinedSchemas`) in one package. Depends on `Axial.Data` and `Axial.ErrorHandling`.
- `Axial.Schema.JsonSchema` (`src/Axial.Schema.JsonSchema/`): JSON Schema generation (`JsonSchema.generate`) in the
  `Axial.Schema` namespace. Depends on `Axial.Schema`.
- `Axial.Schema.Codec` (`src/Axial.Schema.Codec/`): compiled JSON codecs. Depends on `Axial.Schema`.
- `Axial.Schema.Http` (`src/Axial.Schema.Http/`): host-neutral HTTP boundary support — query/form structured data
  (`BoundaryInput`), RFC 9457 problem details from parse diagnostics, and OpenAPI 3.1 documents assembled from
  `EndpointSpec` values. Depends on `Axial.Schema` only; never on `Axial.Flow`.
- `Axial.Schema.Http.AspNetCore` / `Axial.Schema.Http.GenHttp` (`src/Axial.Schema.Http.*/`): host boundaries over
  `Axial.Schema.Http` and `Axial.Flow`. The default API lowers an ordinary endpoint Flow from schema-trusted request
  input through explicit application services to a native response; lower-level `RetainedParseResult` adapters remain for
  redisplay and custom boundaries. Routing and app wiring remain the host's idiom.
- `Axial.Schema.Testing` (`src/Axial.Schema.Testing/`): non-packable FsCheck adapter deriving test data from Schema.
  Depends on `Axial.Schema` and FsCheck; never move the test-library dependency into a public package.
- `Axial.Schema.Contracts` (`src/Axial.Schema.Contracts/`): non-packable wire-tier generation library — the
  `[<DeriveSchema>]` record frontend (`Records.fs`, FCS syntax-only), the `.contract` parser, and the shared
  resolver/emitter. The `Axial.Schema.Derive` attribute namespace lives in `Axial.Schema` itself (inert metadata).
  FCS stays tool-tier only: never referenced from a packable library.
- `Axial.Schema.Contracts.Build` (`src/Axial.Schema.Contracts.Build/`): packable targets-only MSBuild package
  running `scripts/schemagen` before compile over `<AxialDeriveSchema>`/`<AxialContract>` items.
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
- Schema input/rules/interpreters: `src/Axial.Schema/{Model,Data,SchemaValidation,RetainedParseResult,Rules}.fs` and
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
