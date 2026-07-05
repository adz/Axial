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

Phases 19–24 are complete: the two-door narrative, one boundary error, one domain-value catalog, union schemas,
boundary utility packages (`Axial.Codec`, `JsonSchema.generate`, `RawInput.ofJsonDocument`, the `examples/Axial.Api`
minimal-API sample), and positioning/polish (comparison pages, the public AOT/Fable story, the backtick audit with
`Policy.lift`). The `dotnet new axial-api` template is evaluated and deferred in `dev-docs/decisions/README.md`.

## Phase 25: Reiteration Questions

These are open questions produced while completing phases 23–24. Triage each into a concrete task (or delete it with a
one-line decision note in `dev-docs/decisions/README.md`) before starting new feature phases.

- [ ] Codec decode allocations are ~2x `System.Text.Json` (2.84 KB vs 2.01 KB per aggregate) even though speed is at
  parity. Is per-decode slot allocation worth eliminating (pooled slots, struct slots, array-built lists instead of
  cons+rev), aiming to beat STJ the way CodecMapper does?
- [ ] Should `Axial.Codec` grow stream/`PipeWriter`/async entry points (and an ASP.NET Core content-negotiation
  helper) so the sample's `Results.Text(Json.serialize ...)` becomes a one-liner without intermediate strings?
- [ ] Is there a case for a "checked codec" mode that also runs constraint metadata on decode — cheaper than the
  RawInput boundary lane but defensive against misbehaving internal producers?
- [ ] Union wire shape is fixed to `{discriminator, payload}` wrapper objects. Should internally-tagged objects
  (payload fields merged beside the tag) or bare-string enum cases be expressible, and how does that lower to
  JSON Schema?
- [ ] Schema has no optional-field concept: every field is constructor-required, and `optional` is only metadata.
  Should `Value.optionOf`/`Schema.optional` exist so `'field option` models parse and encode (JSON null/absent)
  without workarounds?
- [ ] `JsonSchema.generate` emits a compact, draft-agnostic document. Should it pin `$schema` (2020-12), attach
  titles/descriptions from schema metadata, and hoist repeated nested models into `$defs`?
- [ ] The API sample hand-rolls its HTML form from `Inspect` metadata, duplicating the UiMetadata prototype in tests.
  Promote a small shipped UI-metadata interpreter, or keep form rendering an application concern?
- [ ] `Axial.Codec` carries `FABLE_COMPILER` gates but is not compiled by `scripts/check-fable-js-surface.sh`. Should
  the codec be part of the supported Fable surface, and if so, benchmarked there?
- [ ] The boundary lane costs ~6x the codec (JsonDocument → RawInput → Input.parse). Is a fused fast path worth it —
  parsing straight from `Utf8JsonReader` into diagnostics without materializing `RawInput` — or does redisplay make
  materialization essential by design?
- [ ] `RawInput.ofJsonElement` is net8.0-gated inside `Axial.Validation.Schema`. If netstandard2.1 consumers ask for
  it, add a `System.Text.Json` package reference behind a TFM condition, or split an adapter package?
- [ ] C#-reader friendliness: the recommended samples are clean F#, but should key pages add a short "calling this
  from C#" snippet (compiled codec + parse from C#), given `JsonCodec<'model>`/`ParsedInput` are C#-usable?
- [ ] Docgen now reads `Axial.Validation.Schema` from the net8.0 build so the STJ adapters document; `Axial.Schema`
  still documents from netstandard2.1, hiding `Value.date`/`Schema.date`. Should reference docs standardize on the
  net8.0 surface with "netstandard2.1: unavailable" notes?

## Acceptance Checks

The two-group direction is coherent when the following are true:

- the README and getting started teach exactly two doors: Schema for domain models, plain `Result` for simple code
- a newcomer handles one error shape at the boundary, with one default renderer to display strings
- domain value types exist in one catalog usable standalone and as schema fields
- discriminated unions are expressible as schemas with path-aware diagnostics
- a runnable ASP.NET Core sample serves parsing, error responses, and OpenAPI from one schema declaration
- one schema declaration also compiles to a trusted-lane JSON codec with benchmarked performance
- Flow is never required by the results-group quick starts
- comparison pages answer FsToolkit.ErrorHandling, FluentValidation, and zod by name
- generated reference docs match source comments
