# Axial Tasks

This is the active development queue. Keep completed work out of this file because loop scripts consume it directly.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

Work this queue from top to bottom, with one caveat: the schema surface has just been through heavy churn
(`Model<'t>`/`Model.validate`, `ContextRules`, the contract generator — see `dev-docs/decisions/README.md`,
2026-07-11/12 entries) and the shape is settling, not settled. Phases 29–30 below are current thinking with enough
detail to pick up cold; re-read the decisions file and sanity-check the ordering before starting any of them.

Axial has two main groups, and everything in this queue serves that split:

- **Parse-don't-validate results**: `Schema` is the front door for domain models — parsing, validation, redisplay,
  contextual rules, and metadata fall out of one declaration. Plain `Result` with the user's own error DU is the
  blessed lane for simple code. `Check`, `Validation`, `Refined`, and the interpreter error types are machinery
  behind those two doors, not peer entry points.
- **Effects in Flow**: the ZIO-style Reader-Async-Result workflow model. Useful with or without schemas, and never
  part of the entry price for the results group. Flow-group gaps are tracked in `LATER_TODO.md` (demand-driven, not
  worked top to bottom).

Phases 19–28-prelude are complete and recorded in `dev-docs/decisions/README.md` and git history; the most recent
completions (2026-07-09..12): the `Model` module split with a sound `reconstruct`, `Axial.Refined` moved into
`Axial.ErrorHandling`, `Model<'model>` + `Model.validate` (named-field trusted construction), `FieldRef` +
`ContextRules` (RuleSet deleted), and the `.contract` grammar/generator as wire-tier tooling
(`src/Axial.Schema.Contracts`, `scripts/schemagen`, golden corpus in `tests/Axial.Schema.Tests/contracts/`).

## Phase 29: Schema depth (pre-1.0 candidates from the ZIO Schema comparison)

Source analysis: `dev-docs/current-ideas/zio-schema-comparison.md` (2026-07-11 deep dive). These are the gaps judged
genuinely useful for this project's goals, ranked. Items 1–2 are worth doing before 1.0; item 3 rides along with
anything. None of this is committed scope — the schema surface is still settling.

### 29.1 Recursive schemas (the one expressiveness wall)

The builder cannot express a self-nesting model at all (comment trees, nested config sections, org hierarchies) —
ZIO does it with a `Lazy` node. A user who hits this has no workaround inside Axial.

- Core: a lazy node in the value-definition DU (`src/Axial.Schema/Schema.fs`, `ValueSchemaDefinition.Shape` /
  `ValueShape`) — something like `LazyValueDefinition of (unit -> ValueSchemaDefinition)` with memoized force, plus
  `Value.lazyOf : (unit -> Schema<'model>) -> ValueSchema<'model>` (or on `ValueSchema`) so a schema can reference
  itself through a thunk.
- Touch points to walk (each currently assumes a finite tree): `Model.parse`/`Model.reconstruct` recursion (force
  the thunk per visit — naturally terminates on finite *data*), `Codec` compile (compile-on-first-use with a cache
  keyed by the thunk/definition identity, or cycles hang the compiler), `Inspect` (needs cycle detection — probably
  a `ValueShape.Recursive` marker with an identity/reference rather than infinite expansion), `JsonSchema.generate`
  (this finally forces `$defs` hoisting + `$ref` emission — the docs currently note inlining "cannot fail to
  terminate" precisely because recursion is inexpressible; that note inverts).
- Grammar/generator: recursive contract refs (`contract Category.v1 { children: list Category.v1 }`) — the resolver's
  declare-before-use rule needs a self-reference exception; emitter needs `Value.lazyOf (fun () -> Category.schema)`.
  Can trail the core work.
- Tests: recursive record round-trip through parse/reconstruct/codec; JSON Schema `$ref` output; Inspect terminates;
  stack safety on deep (not just cyclic) data.

### 29.2 Test-data generation from schema

Schema → generators that respect constraints (valid emails, in-range ints, count-bounded lists, union case
selection). Cheap relative to value: immediately useful to adopters' property tests, and lets us fuzz our own
parse/validate/codec round-trips.

- Recommended mechanism (avoids any new construction machinery): generate *constraint-satisfying `RawInput`*, then
  `Model.parse` it — validity is guaranteed by construction because parse enforces everything, including refined
  construction and constructor invariants (retry/shrink on the rare constructor rejection). Walk `Inspect.model`
  metadata to drive generation: `SchemaConstraint` codes (`minLength`/`maxLength`/`between`/`atLeast`/`count*`/
  `multipleOf`/`oneOf`/`email`) map to generator ranges; `pattern` starts as "unsupported — supply a custom
  generator for this field" rather than regex-reversal.
- Decide the target library by what the work adoption target actually uses — FsCheck `Gen<'t>` vs Hedgehog — before
  building; wrap as a separate non-packable project first (`Axial.Schema.Testing`?) so the core stays
  dependency-free.
- Also useful internally: property-test the contract engine's migration chains once Phase 28 lands.

### 29.3 FieldRef setters

`FieldRef` currently carries `Name` + `Get` only; ZIO's `Field` carries `set` too. Add
`Set: 'model -> 'value -> 'model` — enables form editing, patch application, and draft manipulation without record
boilerplate at call sites. Touches: `src/Axial.Schema/FieldRef.fs` (the type is 3 days old; breaking it is fine),
the emitter (`Emitter.fs` Fields section: `Set = fun draft value -> { draft with X = value }`), the three golden
`.g.fs` files + `EmitterGoldenTests` (byte-for-byte), and hand-written `FieldRef` mentions in docs. Small; ride
along with any schema work.

## Phase 30: Contracts milestone bundle (gated on Phase 28 + a real consumer)

From the same ZIO comparison; these belong *with* the remote-config milestone, not before it:

- **Schema-as-data** (their `MetaSchema`): a stable serialized form of `Inspect`'s `ModelDescription` tree, so the
  browser editor receives the schema as *data* and drives forms dynamically instead of compiling every schema into
  the Fable bundle; also the substrate for contract version-diff tooling (the LSP's planned version-gap warnings).
  Note `Inspect` output is already a plain data tree — most of this is choosing a stable wire format + a codec for
  descriptions, not a new representation. Constraint arguments are `obj`-boxed (`SchemaConstraint.tryFindArgument`),
  which is where the serialization design effort actually lives.
- **Diff/Patch**: schema-derived structural diff of two values ("what changed between desired and reported
  config"), rendered over the same `Path` vocabulary as diagnostics so display infrastructure is shared. Read-only
  walk over erased getters suffices for diff; patch application wants `FieldRef.Set` (29.3).
- **Deliberately rejected** from the ZIO list (recorded so nobody re-litigates casually): automatic structural
  migrations (conflicts with manual-typed-migrations; their own docs show it silently deleting fields), advisory
  validation, multi-format codecs before a consumer asks, `DynamicValue` as a public surface (at most internal
  plumbing for the two items above).

## Smaller queue items

- User-facing docs for the contract tooling once it stabilizes: a `docs/schema/contracts.md` guide (grammar by
  example, `schemagen` usage, `--check` in CI, "wire tier only — domain models are hand-written" positioning). The
  only current documentation is dev-docs and the golden corpus.
- `docs/schema/getting-started.md` and the tutorials still teach only the pipe-builder + parse flow; fold in
  `Model.validate` + drafts as the named-field construction story (trusted-construction.md already covers it; the
  tutorials don't).
- `dev-docs/API_BASELINE.md` needs a fresh baseline pass after the 2026-07-09..12 renames (it still references
  pre-consolidation projects and the old `Input`/`Validation.validate`/`RuleSet` names in its notes).

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
