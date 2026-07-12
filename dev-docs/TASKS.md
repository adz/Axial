# Axial Tasks

This is the active development queue. Keep completed work out of this file because loop scripts consume it directly.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

Work this queue from top to bottom, with one caveat: the schema surface has just been through heavy churn
(`Schema.check`, `ContextRules`, the contract generator and versioning engine — see `dev-docs/decisions/README.md`,
2026-07-11..13 entries) and the shape is settling, not settled. Phase 30 below is current thinking with enough
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
completions (2026-07-09..13): the Schema value/model catalog consolidation, `Axial.Refined` moved into
`Axial.ErrorHandling`, `Schema.check` for already assembled typed values, `FieldRef` (with `Set`) +
`ContextRules` (RuleSet deleted), the `.contract` grammar/generator as wire-tier tooling
(`src/Axial.Schema.Contracts`, `scripts/schemagen`, golden corpus in `tests/Axial.Schema.Tests/contracts/`),
the `Contract<'model>` versioning engine (`Contract.parse`/`Contract.parseVersion`, typed contiguous n-1 → n
migrations), `Schema.defer` recursion with finite inspection and `$defs`-based JSON Schema output, and the
non-packable `Axial.Schema.Testing` FsCheck adapter (`SchemaGen`).

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
  walk over erased getters suffices for diff; patch application uses `FieldRef.Set`, which has shipped.
- **Deliberately rejected** from the ZIO list (recorded so nobody re-litigates casually): automatic structural
  migrations (conflicts with manual-typed-migrations; their own docs show it silently deleting fields), advisory
  validation, multi-format codecs before a consumer asks, `DynamicValue` as a public surface (at most internal
  plumbing for the two items above).

## Smaller queue items

- User-facing docs for the contract tooling once it stabilizes: a `docs/schema/contracts.md` guide (grammar by
  example, `schemagen` usage, `--check` in CI, "wire tier only — domain models are hand-written" positioning). The
  only current documentation is dev-docs and the golden corpus.
- Review the tutorials against the construction split: `Schema.check` for admitting already assembled public
  wire/draft values; private representations plus authoritative smart constructors for durable domain guarantees;
  separate drafts only for named assembly/editing of private cross-field aggregates. `trusted-construction.md` already
  contains the detailed treatment.
- `dev-docs/API_BASELINE.md` needs a fresh validated-command pass: its project lists were corrected on 2026-07-12,
  but the recorded run, test counts, and baseline commit predate the 2026-07-09..13 renames.
- Decide whether `Check` should expose SRTP-based common names (`Check.present` and friends resolving across
  `String`/`Option`/`ValueOption`/`Nullable`) instead of requiring the nested-module qualification; today only the
  nested forms (`Check.String.present`, `Check.Option.present`, ...) exist.

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
