# Axial Schema/Input/Check/Rules/Policy TODO

This is the implementation loop for the consolidated data-boundary direction in
`dev-docs/current-ideas/schema-and-rules.md`.

Work this list top to bottom. Each item should be small enough to become an issue or a focused implementation pass.

## Phase 0: Align The Architecture Docs

- [x] Update `AGENTS.md` architecture invariants so `Check` is no longer defined as pure `bool` predicates.
- [x] Update `dev-docs/decisions/README.md` to record the new direction:
  `Check<'value> = 'value -> Result<unit, CheckFailure list>`.
- [x] Update `dev-docs/project-split.md` so package responsibilities include complete typed `Check`, `Axial.Schema`,
  schema interpreters, and `Policy`.
- [x] Remove or rewrite stale current-ideas notes that still require predicate-only `Check`, especially
  `dev-docs/current-ideas/check-and-result-splits.md`.
- [x] Reconcile `dev-docs/TASKS.md` with this plan, including the package-boundary test split already listed there.
- [x] Decide whether `Axial.Schema` starts as a new project immediately or begins inside `Axial.Validation` behind the
  future package boundary: start `Axial.Schema` as a separate project immediately when schema source work begins.
- [x] Record the package-boundary decision before any source work starts.

## Phase 1: Redesign Check As A Complete Subsystem

- [x] Define `Check<'value> = 'value -> Result<unit, CheckFailure list>`.
- [x] Define `CheckFailure` with at least missing/blank, invalid format, length, range, count, equality, and custom-code
  cases.
- [x] Implement top-level composition:
  `Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure`.
- [x] Decide and document whether `Check.all []` succeeds and whether `Check.any []` fails.
- [x] Implement `Check.String`:
  `present`, `minLength`, `maxLength`, `lengthBetween`, `email`, `matches`, `oneOf`, and null/blank behavior.
- [x] Implement `Check.Number`:
  `between`, `greaterThan`, `lessThan`, `atLeast`, and `atMost`.
- [x] Decide whether numeric helpers need separate modules for `int`, `decimal`, `float`, date/time, or generic
  comparison is enough for the first pass.
- [x] Implement `Check.Collection`:
  `notEmpty`, `minCount`, `maxCount`, `countBetween`, and `distinct`.
- [x] Implement `Check.Option`:
  `some` and `none`.
- [x] Implement `Check.Result`:
  `ok` and `error`.
- [x] Decide whether nullable/value-option checks belong in first pass: include both as first-pass typed checks.
- [x] Update or replace current API shape tests that assert `Check` returns `bool`.
- [x] Add behavior tests for composition, error accumulation, short-circuit policy if any, null-sensitive strings, ranges,
  collections, options, and results.
- [x] Update source comments for the new `Check` model.

## Phase 2: Stabilize Result/Parse/Refine Around New Check

- [x] Decide which existing `Result` helpers remain as fail-fast guards over `Check`.
- [x] Align `Result.guard`, `Result.require`, and type-preserving guards with `Check<'value>`.
- [x] Ensure `Parse` remains in `Axial.Refined` for text-to-primitive conversion.
- [x] Ensure refined constructors can use `Check` programs without depending on schema.
- [x] Add examples of refined/domain types using `Check.String.*` and `Check.Number.*`.
- [ ] Confirm `Axial.Refined` does not depend on `Axial.Validation` or `Axial.Schema`.
- [ ] Update affected tests and API baselines.

## Phase 3: Introduce Schema Core

- [ ] Define `Schema<'model>`.
- [ ] Define `ValueSchema<'value>`.
- [ ] Define `Field<'model, 'value>` metadata.
- [ ] Model external field names.
- [ ] Model getters for existing model inspection.
- [ ] Model constructor application for trusted construction.
- [ ] Model field ordering explicitly and test it heavily.
- [ ] Define primitive value schemas:
  text, int, decimal, bool, date/date-time where supported, and GUID.
- [ ] Define schema constraints as metadata, not just executable checks.
- [ ] Implement schema constraints for:
  `required`, `optional`, `minLength`, `maxLength`, `lengthBetween`, `email`, `pattern`, `oneOf`, numeric ranges,
  collection counts, and distinctness.
- [ ] Ensure schema constraints can lower to executable `Check` programs.
- [ ] Ensure schema constraints retain metadata for diagnostics, JSON Schema, UI, and docs.
- [ ] Define an explicit core API before computation expressions:
  `Schema.field`, `Schema.map2`, `Schema.map3`, and enough `mapN` helpers to prove the model.
- [ ] Decide how many `mapN` helpers are acceptable before requiring generator support.
- [ ] Add tests proving constructor/getter alignment behavior.
- [ ] Add tests proving schema constraints are inspectable without running validation.

## Phase 4: Add Refined Value Schemas

- [ ] Define `Value.refined` or equivalent for named refined/domain types.
- [ ] Require both construction and inspection functions for refined value schemas.
- [ ] Support refined schemas over primitive schemas, especially text.
- [ ] Support `format` metadata such as `email`.
- [ ] Ensure refined value schemas can run `Check` programs.
- [ ] Ensure model schemas can use `field "email" _.Email Email.schema { required }`.
- [ ] Add examples for `Email`, `ContactName`, positive/non-negative numbers, and bounded strings.
- [ ] Decide which refined schemas ship in `Axial.Refined` versus examples only.

## Phase 5: Build RawInput And Input Parsing

- [ ] Define source-agnostic `RawInput`:
  `Missing`, `Scalar`, `Many`, and `Object`.
- [ ] Implement path addressing for names and indexes.
- [ ] Implement raw value lookup by paths like `contacts[1].value`.
- [ ] Implement raw redisplay helpers.
- [ ] Implement adapters:
  map, name-value collection, CLI args, JSON-like value, and configuration.
- [ ] Define `ParsedInput<'model, 'error>`.
- [ ] Add helpers:
  `IsValid`, `Model`, `TryModel`, `Errors`, `ErrorsFor`, `Input`, and `Result`.
- [ ] Implement `Input.parse : Schema<'model> -> RawInput -> ParsedInput<'model, SchemaError>`.
- [ ] Make `required` reject missing raw fields and missing/blank scalar values.
- [ ] Ensure field errors accumulate applicatively.
- [ ] Ensure failed parses retain raw input.
- [ ] Ensure constructors are not called when intrinsic field parsing fails.
- [ ] Add tests for successful parse, failed parse, multiple sibling errors, raw redisplay, and field error lookup.

## Phase 6: Define Schema Errors And Diagnostics Interpretation

- [ ] Define `SchemaError`.
- [ ] Include required, expected scalar/object/many, invalid format, too short, too long, out of range, count failures,
  duplicate failures, constructor failures, and custom code/message.
- [ ] Map `CheckFailure` to `SchemaError`.
- [ ] Attach errors to `Diagnostics<'error>` paths.
- [ ] Keep field names out of `SchemaError` when diagnostics path already carries them.
- [ ] Support custom messages on schema constraints.
- [ ] Support mapping schema/input errors to domain/application errors.
- [ ] Add tests for path rendering and flattened diagnostics.

## Phase 7: Nested Models And Collections

- [ ] Implement `nested "address" _.Address Address.schema { required }`.
- [ ] Ensure nested schemas use getters for inspection interpreters.
- [ ] Ensure nested input expects object-shaped raw input.
- [ ] Prefix nested diagnostics with `PathSegment.Name`.
- [ ] Implement `many "contacts" _.Contacts ContactMethod.schema { minCount 1 }`.
- [ ] Ensure collection input expects `RawInput.Many`.
- [ ] Parse every item and accumulate every item error.
- [ ] Prefix collection diagnostics with `PathSegment.Index`.
- [ ] Support raw redisplay paths such as `contacts[1].value`.
- [ ] Add tests for nested success, nested failure, collection success, multiple item failures, and count constraints.

## Phase 8: Constructor-Level Intrinsic Errors

- [ ] Support `schemaResult` or equivalent for constructors returning `Result`.
- [ ] Attach constructor errors at root by default.
- [ ] Support attaching constructor errors to a field path with `Input.constructorErrorAt "end"` or equivalent.
- [ ] Decide how constructor errors compose with field errors.
- [ ] Add `DateRange`-style tests for cross-field intrinsic invariants.
- [ ] Document the difference between constructor invariants and contextual rules.

## Phase 9: Validation Interpreter For Existing Models

- [ ] Implement `Validation.validate : Schema<'model> -> 'model -> Validation<'model, SchemaError>` or equivalent.
- [ ] Use getters, not raw input.
- [ ] Reuse schema constraints and `Check` lowering.
- [ ] Validate nested models through nested schemas.
- [ ] Validate collections through item schemas.
- [ ] Ensure values created by `Input.parse` normally pass intrinsic validation.
- [ ] Add tests for imported/hand-built values and generated-builder values.

## Phase 10: Contextual Rules

- [ ] Define `RuleSet<'model, 'error>`.
- [ ] Implement `rules<'model> { ... }` or explicit core API.
- [ ] Support field/path attachment for rule failures.
- [ ] Support custom code and message.
- [ ] Support rules over already-trusted models.
- [ ] Implement `Rules.apply : RuleSet<'model, 'error> -> 'model -> Result<'model, Diagnostics<'error>>`.
- [ ] Ensure rules do not construct models.
- [ ] Add examples and tests for support-ticket and approval-style workflow rules.
- [ ] Document the distinction between schema constraints and rules.

## Phase 11: Policy And Flow Integration

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

## Phase 12: Non-Validation Interpreters

- [ ] Define an inspection API over `Schema<'model>`.
- [ ] Prototype `JsonSchema.generate`.
- [ ] Lower required, max length, min length, pattern, format, enum/oneOf, numeric ranges, and collection counts to JSON
  Schema metadata.
- [ ] Prototype `Docs.describe`.
- [ ] Prototype UI metadata description without creating a UI framework.
- [ ] Decide how CodecMapper should consume schema without taking a dependency cycle.
- [ ] Add tests that prove schema metadata can be inspected without running validation.

## Phase 13: Computation Expression DSL

- [ ] Design `schema create { ... }` over the explicit core.
- [ ] Implement primitive field operations:
  `text`, `int`, `decimal`, `bool`, `date`, and `guid`.
- [ ] Implement generic `field "email" _.Email Email.schema { ... }`.
- [ ] Implement `nested`.
- [ ] Implement `many`.
- [ ] Implement field constraint blocks.
- [ ] Confirm external-name-first ordering remains the public style.
- [ ] Add examples comparing Rails ActiveModel and Axial schema side by side.
- [ ] Add compile/API shape tests for the DSL.

## Phase 14: Source Generation Later

- [ ] Defer runtime reflection as a foundation.
- [ ] Design source generation only after explicit schema and DSL APIs stabilize.
- [ ] Prototype `[<Schema>]` record generation.
- [ ] Generate constructor/getter alignment where possible.
- [ ] Generate schemas for primitive field attributes such as required, max length, email, and min length.
- [ ] Decide whether generated schemas can target private constructors safely.

## Phase 15: Documentation And Examples

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

## Phase 16: Cleanup

- [ ] Delete stale design notes after decisions are promoted.
- [ ] Refresh `dev-docs/API_BASELINE.md`.
- [ ] Split tests into package-specific projects that mirror the package boundaries:
  - `Axial.Flow.Tests` for `Flow`, `Layer`, `Scope`, `BindError`, `Policy`, runtime mechanics, and `Axial.Flow.*`
    service packages.
  - `Axial.ErrorHandling.Tests` for `Check`, `CheckFailure`, `Result`, `Collection`, and `result { }`.
  - `Axial.Refined.Tests` for `Parse`, `Refine`, refined/domain values, and `refine { }`.
  - `Axial.Validation.Tests` for `Validation`, `Diagnostics`, paths, and `validate { }`.
  - `Axial.Schema.Tests` for schema/value-schema definitions, field metadata, constructor/getter descriptors,
    constraint metadata, and field ordering.
  - `Axial.Validation.Schema.Tests` for input parsing, schema validation, rules, `SchemaError`, and diagnostics
    interpretation.
  - `Axial.Tests` only for umbrella-package smoke tests and cross-package integration scenarios.
- [ ] Ensure package dependency rules are enforced by tests.
- [ ] Ensure generated docs and examples do not teach old predicate-only `Check`.
- [ ] Ensure docs do not present schema as only validation.
- [ ] Ensure docs do not present invalid domain objects as normal.
