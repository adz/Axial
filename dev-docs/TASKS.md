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

Phase 25 (reiteration-question triage) is complete: all twelve questions from `dev-docs/questions.md` were decided.
Deferred items (codec decode allocations, checked-codec mode, UI-metadata promotion, fused boundary path, netstandard2.1
STJ adapter) are recorded with their pre-chosen answers in `dev-docs/decisions/README.md`; the rest became Phase 26.

## Phase 26: Triaged Boundary Work

Ordered cheap-and-high-leverage first. Items 1–3 are also prerequisites for the contract grammar
(`dev-docs/current-ideas/contract-grammar.md`).

- [ ] Optional fields: add `Value.optionOf : ValueSchema<'value> -> ValueSchema<'value option>` so `'field option`
  models are schema-describable. `Input.parse`: missing/null → `Ok None`, present → `Some` (constraints run on the
  payload). Codec: absent/null decodes to `None`; `None` encodes as *omitted* (no `null` policy pre-1.0). JSON Schema:
  optional fields drop out of `required` — this also fixes the existing mismatch where `required` lists only fields
  carrying the `required` constraint while the parser requires everything. Forbid `optionOf (optionOf ...)` and
  combining `optionOf` with the `required` constraint at build time.
- [ ] Union wire shapes: add `Value.enumOf` (bare-string enums for payload-less DU cases, lowering to JSON Schema
  `enum`) and `Value.unionInline` (internally-tagged objects, serde/zod style — valid only when every payload is an
  object whose field names don't collide with the discriminator, checked at construction; lowers to `oneOf` members
  with a `const` discriminator beside payload properties). Cover all three interpreters (Input.parse, Codec,
  JsonSchema) plus Inspect descriptions.
- [ ] JSON Schema fidelity: pin `"$schema"` to draft 2020-12, add description metadata (`Value.describe` /
  `Schema.describe` authoring surface), and emit it as `title`/`description`. `$defs` hoisting stays deferred (see
  decisions).
- [ ] Docgen target skew: standardize all docgen inputs on `net8.0` builds; audit TFM-gated members
  (`Value.date`/`Schema.date`, STJ adapters) and add "netstandard2.1: not available" lines to their XML remarks so the
  reference describes one coherent surface.
- [ ] Codec stream entry points: `Json.serializeToStream` (sync, flushed once) and `Json.deserializeStreamAsync`
  (read-to-end into a pooled buffer, then decode — no incremental streaming pre-1.0). `Axial.Codec` stays
  dependency-free; ASP.NET Core conveniences stay in the `examples/Axial.Api` sample. Update the sample so the
  response path no longer materializes an intermediate string.
- [ ] Fable codec surface: add `Axial.Codec` to `scripts/check-fable-js-surface.sh` and add a Node round-trip
  (encode → decode) test so the `FABLE_COMPILER` gates are exercised, then claim codec-on-Fable in the zod comparison
  ("one declaration shared between server and browser" now includes serialization).
- [ ] C# ergonomics audit: verify which `Json.*`/`Input.*` entry points surface as clean static methods from C# versus
  `FSharpFunc` chains; add `[<CompiledName>]`/tupled overloads or members (e.g. `JsonCodec.Deserialize(string)`) where
  needed, then add a short "From C#" section to the codec and input-sources pages. Consume-don't-author is the story:
  F# declares schemas, C# compiles codecs, parses, and reads diagnostics.

## Phase 27: Contract Grammar Prerequisites

From `dev-docs/current-ideas/contract-grammar.md` sequencing step 1 — each useful independently of the grammar. Do
these after Phase 26 items 1–3, which the grammar also needs (`?` optionality, literal unions, `///` doc comments).

- [ ] Add `Value.map : ValueSchema<'value> -> ValueSchema<Map<string,'value>>` (JSON objects as dictionaries; keys are
  always text) across Input.parse, Codec, JsonSchema (`additionalProperties`), and Inspect.
- [ ] Add default-value metadata (`= literal` in the grammar; also wanted by the config-editor story) as schema
  metadata with JSON Schema `default` lowering.
- [ ] Add a `multipleOf` schema constraint lowering to the existing constraint machinery.

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
