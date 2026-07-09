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

Phase 26 (triaged boundary work) is complete: union wire shapes (`Value.enumOf`, `Value.unionInline`) across
Input.parse, Codec, JsonSchema, and Inspect, and the C# ergonomics audit (`RawInput.ofDictionary`,
`RawInput.ofConfigurationPairs`, `Input.parseWithOptions`, and "From C#" sections on the codec and input-sources
pages).

## Phase 27: Contract Grammar Prerequisites

From `dev-docs/current-ideas/contract-grammar.md` sequencing step 1 — each useful independently of the grammar.

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
