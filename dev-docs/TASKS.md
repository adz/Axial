# Axial Tasks

This is the active development queue. Keep completed work out of this file because loop scripts consume it directly.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

Work this queue from top to bottom.

Axial has two main groups, and everything in this queue serves that split:

- **Parse-don't-validate results**: `Schema` is the front door for domain models — parsing, validation, redisplay,
  rules, and metadata fall out of one declaration. Plain `Result` with the user's own error DU is the blessed lane for
  simple code. `Check`, `Validation`, `Refined`, and the interpreter error types are machinery behind those two doors,
  not peer entry points.
- **Effects in Flow**: the ZIO-style Reader-Async-Result workflow model. Useful with or without schemas, and never part
  of the entry price for the results group.

## Phase 19: Narrative Inversion

Make the two-group story the public story. No new APIs; docs and positioning only.

Done as a docs restructuring with two landing pages: `/parse/` ("Parse, don't validate", blue) and `/flow/`
("Structured workflows without framework lock-in", violet), sourced from `docs/landing/`, plus a minimal two-door
root page. Each landing cross-links the other exactly once via a tinted strip at the boundary moment
(`Flow.verify` / boundary parsing).

- [x] Rewrite the README opening around the two groups: schema-first for domain models, plain `Result` for simple
  code, Flow as the optional effects group.
- [x] Replace the "choose the smallest tool" ladder in `docs/start/getting-started.md` with the two-lane rule; move
  the tool-ladder explanation to an advanced/architecture page.
- [x] Bless plain `Result` with a user-owned error DU as idiomatic for simple code in docs
  (not a compromise; `Check` only when structured failure details pay for themselves).
- [x] Reframe docs section indexes so Check/Validation/Refined read as machinery chapters
  ("how parsing works underneath"), with Schema and Error Handling (plain Result) as the two doors.
- [x] Update `llms.txt` and `docs/AGENT.md` to teach the two-lane rule first.
- [x] Keep Flow messaging separable: schema/results usable standalone; no Flow types in the results quick starts.
- [x] Run `bash scripts/validate-docs.sh`.

## Phase 20: One Boundary Error

Collapse the failure taxonomies a newcomer meets. Pre-1.0 breaking changes are acceptable here.

- [x] Design the single boundary error story: `ParseError`, `RefinementError`, and `CheckFailure` lower into one
  boundary error shape (either `SchemaError` or a shared type it embeds).
- [x] Provide one default English renderer for the boundary error with a one-liner from any failed parse/validation to
  display strings; keep custom-message overrides.
- [x] Ensure `ParsedInput`, `Rules`, refined construction, and `Parse` failures all reach that renderer without
  per-subsystem mapping ceremony.
- [x] Keep user-owned error DUs first-class: mapping from the boundary error into a domain error stays a single
  function application at the boundary.
- [x] Update the error-handling and schema docs to present one taxonomy of failure at the boundary.
- [x] Add tests covering lowering from each source error type and default rendering.

## Phase 21: One Catalog Of Domain Values

Merge the Refined catalog and schema refined values into a single artifact.

- [ ] Ship ready-made schemas for the Refined catalog types. Scalar schemas now live in
  `Axial.Validation.Schema.RefinedSchema` to preserve the `Axial.Refined` leaf-package boundary; remaining work is
  collection/domain-range support once schema core can express arbitrary item collection schemas and record-shaped
  catalog schemas ergonomically.
- [ ] Make standalone refinement (`Refine.positiveInt`) and schema-field use the same underlying constraint metadata,
  eliminating the `Refine.positiveInt` vs `SchemaConstraint.greaterThan 0` duplication.
- [ ] Decide and document the single home for authoring new domain value types (one page: private ctor +
  `Value.refined` + optional standalone helpers).
- [ ] Update the Refined catalog docs and schema refined-values guide to point at one catalog.
- [ ] Add tests proving a catalog type behaves identically standalone and as a schema field.

## Phase 22: Union Schemas

Discriminated unions are how F# users model domains; schema must express them.

- [ ] Design a tagged/choice schema shape for discriminated unions
  (e.g. `Payment = Card of CardDetails | Invoice of InvoiceDetails`), including the raw-input discriminator
  convention (tag field, wrapper object, or configurable).
- [ ] Support case payloads that are nested model schemas, refined values, or primitives.
- [ ] Parse union input with path-aware diagnostics (wrong tag, missing payload, payload field errors under the case
  path).
- [ ] Validate existing union values through case getters.
- [ ] Expose unions through `Inspect` (case names, per-case descriptions) and lower to JSON Schema `oneOf` in the
  prototype interpreter.
- [ ] Add tutorials and tests for a union-heavy domain model.

## Phase 23: Boundary Utility Packages

Convert the "one schema drives everything" pitch from prototypes into shippable utility.

- [ ] Add a `System.Text.Json` adapter: `RawInput.ofJsonElement` / `ofJsonDocument` (own package or gated module so
  core stays dependency-free and Fable-safe).
- [ ] Promote `JsonSchema.generate` from test prototype to a real module/package over `Inspect`.
- [ ] Build a complete ASP.NET Core minimal-API sample: JSON body in, 400-with-path-diagnostics or trusted model out,
  OpenAPI/JSON Schema served from the same schema declaration, plus a form redisplay page.
- [ ] Evaluate a `dotnet new` template (`axial-api`) seeded from that sample.
- [ ] Keep the sample buildable in CI and mirrored into the runnable-examples docs page.

## Phase 24: Positioning And Polish

Answer the incumbents by name and remove newcomer friction.

- [ ] Write comparison pages: vs FsToolkit.ErrorHandling (combinators are functions; schema is inspectable data),
  vs FluentValidation (validators check existing objects; Axial never constructs the invalid object),
  vs zod (same philosophy; AOT/Fable-safe, no reflection).
- [ ] Surface the zero-reflection / AOT / trimming / Fable story in public docs (currently buried in dev-docs).
- [ ] Audit backtick names out of the recommended public surface and docs samples
  (`Value.``int```, `Policy.``pure```; provide non-keyword aliases or adjust guidance).
- [ ] Review recommended samples for C#-reader friendliness (symbol noise, inference failures, error-message quality).
- [ ] Run `bash scripts/validate-docs.sh` and refresh `dev-docs/API_BASELINE.md` after any surface changes.

## Acceptance Checks

The two-group direction is coherent when the following are true:

- the README and getting started teach exactly two doors: Schema for domain models, plain `Result` for simple code
- a newcomer handles one error shape at the boundary, with one default renderer to display strings
- domain value types exist in one catalog usable standalone and as schema fields
- discriminated unions are expressible as schemas with path-aware diagnostics
- a runnable ASP.NET Core sample serves parsing, error responses, and OpenAPI from one schema declaration
- Flow is never required by the results-group quick starts
- comparison pages answer FsToolkit.ErrorHandling, FluentValidation, and zod by name
- generated reference docs match source comments
