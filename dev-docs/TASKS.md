# Axial Tasks

This is the active development queue. Keep completed work out of this file because loop scripts consume it directly.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

Work this queue from top to bottom.

## Phase 7: Build RawInput And Input Parsing

- [x] Define source-agnostic `RawInput`:
  `Missing`, `Scalar`, `Many`, and `Object`.
- [x] Implement path addressing for names and indexes.
- [x] Implement raw value lookup by paths like `contacts[1].value`.
- [x] Implement raw redisplay helpers.
- [x] Implement adapters:
  map, name-value collection, CLI args, JSON-like value, and configuration.
- [x] Define `ParsedInput<'model, 'error>`.
- [x] Add helpers:
  `IsValid`, `Model`, `TryModel`, `Errors`, `ErrorsFor`, `Input`, and `Result`.
- [x] Implement `Input.parse : Schema<'model> -> RawInput -> ParsedInput<'model, SchemaError>`.
- [x] Make `required` reject missing raw fields and missing/blank scalar values.
- [x] Ensure field errors accumulate applicatively.
- [x] Ensure failed parses retain raw input.
- [x] Ensure constructors are not called when intrinsic field parsing fails.
- [x] Add tests for successful parse, failed parse, multiple sibling errors, raw redisplay, and field error lookup.

## Phase 8: Define Schema Errors And Diagnostics Interpretation

- [x] Keep `SchemaError` and schema diagnostics interpretation in `Axial.Validation.Schema`, not core `Axial.Schema`.
- [x] Define `SchemaError`.
- [x] Include required, expected scalar/object/many, invalid format, too short, too long, out of range, count failures,
  duplicate failures, constructor failures, and custom code/message.
- [x] Map `CheckFailure` to `SchemaError`.
- [x] Attach errors to `Diagnostics<'error>` paths.
- [x] Keep field names out of `SchemaError` when diagnostics path already carries them.
- [x] Support custom messages on schema constraints.
- [x] Support mapping schema/input errors to domain/application errors.
- [x] Add tests for path rendering and flattened diagnostics.

## Phase 9: Nested Models And Collections

- [x] Implement `nested "address" _.Address Address.schema { required }`.
- [x] Ensure nested schemas use getters for inspection interpreters.
- [x] Ensure nested input expects object-shaped raw input.
- [x] Prefix nested diagnostics with `PathSegment.Name`.
- [x] Implement `many "contacts" _.Contacts ContactMethod.schema { minCount 1 }`.
- [x] Ensure collection input expects `RawInput.Many`.
- [x] Parse every item and accumulate every item error.
- [x] Prefix collection diagnostics with `PathSegment.Index`.
- [x] Support raw redisplay paths such as `contacts[1].value`.
- [x] Add tests for nested success, nested failure, collection success, multiple item failures, and count constraints.

## Phase 10: Constructor-Level Intrinsic Errors

- [x] Support `schemaResult` or equivalent for constructors returning `Result`.
- [x] Attach constructor errors at root by default.
- [x] Support attaching constructor errors to a field path with `Input.constructorErrorAt "end"` or equivalent.
- [x] Decide how constructor errors compose with field errors.
- [x] Add `DateRange`-style tests for cross-field intrinsic invariants.
- [x] Document the difference between constructor invariants and contextual rules.

## Phase 11: Validation Interpreter For Existing Models

- [x] Keep schema-based validation interpreters in `Axial.Validation.Schema`, not core `Axial.Validation`.
- [x] Implement `Validation.validate : Schema<'model> -> 'model -> Validation<'model, SchemaError>` or equivalent.
- [x] Use getters, not raw input.
- [x] Reuse schema constraints and `Check` lowering.
- [x] Validate nested models through nested schemas.
- [ ] Validate collections through item schemas.
- [ ] Ensure values created by `Input.parse` normally pass intrinsic validation.
- [ ] Add tests for imported/hand-built values and generated-builder values.

## Phase 12: Contextual Rules

- [ ] Keep schema/contextual rules in `Axial.Validation.Schema` unless a separate rules package is deliberately created.
- [ ] Define `RuleSet<'model, 'error>`.
- [ ] Implement `rules<'model> { ... }` or explicit core API.
- [ ] Support field/path attachment for rule failures.
- [ ] Support custom code and message.
- [ ] Support rules over already-trusted models.
- [ ] Implement `Rules.apply : RuleSet<'model, 'error> -> 'model -> Result<'model, Diagnostics<'error>>`.
- [ ] Ensure rules do not construct models.
- [ ] Add examples and tests for support-ticket and approval-style workflow rules.
- [ ] Document the distinction between schema constraints and rules.

## Phase 13: Policy And Flow Integration

- [ ] Define or update `Policy<'env, 'error, 'input, 'output> =
  'env -> 'input -> Result<'output, 'error>`.
- [ ] Implement `Policy.pure`.
- [ ] Implement `Policy.withError`.
- [ ] Implement `Policy.context`.
- [ ] Implement `Policy.pass`.
- [ ] Implement `Policy.compose`.
- [ ] Implement `Policy.optional`.
- [ ] Implement `Flow.verify`.
- [ ] Ensure `Policy` lives in `Axial.Flow`.
- [ ] Ensure `Axial.Flow` does not depend on `Axial.Schema`, `Axial.Refined`, or `Axial.Validation`.
- [ ] Add policy examples for parsing, refined construction, schema input result, validation result, and contextual rules.
- [ ] Add tests proving `Flow.verify` injects the current environment and short-circuits on failure.
- [ ] Add tests for context-aware and optional policies.
- [ ] Document when to use `Bind.error`, `Bind.mapError`, and `Policy`.

## Phase 14: Non-Validation Interpreters

- [ ] Define an inspection API over `Schema<'model>`.
- [ ] Prototype `JsonSchema.generate`.
- [ ] Lower required, max length, min length, pattern, format, enum/oneOf, numeric ranges, and collection counts to JSON
  Schema metadata.
- [ ] Prototype `Docs.describe`.
- [ ] Prototype UI metadata description without creating a UI framework.
- [ ] Decide how CodecMapper should consume schema without taking a dependency cycle.
- [ ] Add tests that prove schema metadata can be inspected without running validation.

## Phase 15: Computation Expression DSL

The progressive typed builder from Phase 5b is the explicit core and already scales to any field count, so the
computation expression is optional sugar, not the path past three fields. Ship it only if it beats the pipeline on
readability and compile-error quality for constraint blocks.

- [ ] Design `schema create { ... }` as sugar over the Phase 5b builder core; compare CE ergonomics and compile-error
  quality against the plain pipeline before committing to ship it.
- [ ] Implement primitive field operations:
  `text`, `int`, `decimal`, `bool`, `date`, and `guid`.
- [ ] Implement generic `field "email" _.Email Email.schema { ... }`.
- [ ] Implement `nested`.
- [ ] Implement `many`.
- [ ] Implement field constraint blocks.
- [ ] Confirm external-name-first ordering remains the public style.
- [ ] Add examples comparing Rails ActiveModel and Axial schema side by side.
- [ ] Add compile/API shape tests for the DSL.

## Phase 16: Source Generation Later

- [ ] Defer runtime reflection as a foundation.
- [ ] Design source generation only after explicit schema and DSL APIs stabilize.
- [ ] Prototype `[<Schema>]` record generation.
- [ ] Generate constructor/getter alignment where possible.
- [ ] Generate schemas for primitive field attributes such as required, max length, email, and min length.
- [ ] Decide whether generated schemas can target private constructors safely.

## Phase 17: Documentation And Examples

- [ ] Update architecture docs after the direction is accepted.
- [ ] Update source comments for changed public APIs.
- [ ] Write a guide: ActiveModel ergonomics, F# trusted construction.
- [ ] Write a guide: Schema vs Input vs Check vs Rules vs Policy.
- [ ] Write a guide: refined/domain value schemas.
- [ ] Write a guide: raw redisplay and field errors.
- [ ] Write a guide: contextual rules and Flow policies.
- [ ] Write examples for HTTP form-like input, CLI input, JSON/config input, nested models, collections, and workflow policy.
- [ ] Regenerate reference docs when public APIs exist.
- [ ] Run `bash scripts/validate-docs.sh` after user-facing docs or API comments change.

## Phase 18: Cleanup

- [ ] Delete stale design notes after decisions are promoted.
- [ ] Refresh `dev-docs/API_BASELINE.md`.
- [ ] Ensure package dependency rules are enforced by tests.
- [ ] Ensure generated docs and examples do not teach old predicate-only `Check`.
- [ ] Ensure docs do not present schema as only validation.
- [ ] Ensure docs do not present invalid domain objects as normal.

## Acceptance Checks

The current architecture is coherent when the following are true:

- public docs describe services as explicit and the runtime as executor-only
- user-facing workflow signatures show real service requirements in `'env`
- app/domain dependency examples start with records and `Flow.read`
- reusable service examples use `Service<'service>.get()`
- host-edge examples use `Service<'service>.resolve()` or provider-backed layers
- `Layer` is the documented provisioning mechanism
- registry-backed runtime is gone from both code and docs
- generated reference docs match source comments
