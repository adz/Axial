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
- [x] Promote the accepted schema/input/rules direction into `dev-docs/PLAN.md` so it is not only a current-ideas sketch.
- [x] Create the `Axial.Validation.Schema` integration project before raw input, schema diagnostics, schema validation,
  or schema rules source work begins.
- [x] Decide refined schema ownership: `Axial.Refined` must not ship schema-valued APIs while it remains independent of
  `Axial.Schema`; refined schemas belong in examples, user code, or a future integration package.

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
- [x] Confirm `Axial.Refined` does not depend on `Axial.Validation` or `Axial.Schema`.
- [x] Update affected tests and API baselines.

## Phase 3: Introduce Schema Core Foundation

- [x] Define `Schema<'model>`.
- [x] Define `ValueSchema<'value>`.
- [x] Define `Field<'model, 'value>` metadata.
- [x] Model external field names.
- [x] Model getters for existing model inspection.
- [x] Model constructor application for trusted construction.
- [x] Model field ordering explicitly and test it heavily.
- [x] Define primitive value schemas:
  text, int, decimal, bool, date/date-time where supported, and GUID.
- [x] Define schema constraints as metadata, not just executable checks.

## Phase 4: Tighten Check Module Ergonomics

Lift the design in `dev-docs/tighten-check-module-and-funcs.md` into source before continuing the next schema slice.
Schema constraints should lower onto the tightened `Check` shape, not the verbose transitional one.

- [x] Keep `Check<'value> = 'value -> Result<unit, CheckFailure list>` as the public structured check model.
- [x] Keep `CheckFailure` path-free and raw-input-free; do not move schema/input diagnostics into `CheckFailure`.
- [x] Keep `Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure` as top-level structured check combinators.
- [x] Rename or replace `Check.Collection` with `Check.Seq` for direct sequence-shaped checks.
- [x] Decide whether to retain `Check.Collection` as a short-lived compatibility alias; because Axial is pre-1.0,
  prefer removing it once call sites and docs are updated.
- [x] Keep direct implementation modules:
  `Check.String`, `Check.Number`, `Check.Seq`, `Check.Option`, `Check.ValueOption`, `Check.Nullable`, and
  `Check.Result`.
- [x] Add top-level concrete structured check aliases for common single-target checks:
  `length`, `minLength`, `maxLength`, `lengthBetween`, `email`, `matches`, `oneOf`, `between`, `greaterThan`,
  `lessThan`, `atLeast`, `atMost`, `count`, `minCount`, `maxCount`, `countBetween`, `distinct`, `contains`,
  `single`, `atMostOne`, `atLeastOne`, `moreThanOne`, `equalTo`, and `notEqualTo`.
- [x] Add `Check.String.length` for exact string length.
- [x] Add `Check.Seq.count` for exact sequence count.
- [x] Add structured string checks missing from the direct module:
  `empty`, `notEmpty`, `numeric`, and `alphaNumeric`.
- [x] Add structured numeric sign checks to `Check.Number` and top-level `Check`:
  `positive`, `nonNegative`, `negative`, and `nonPositive`.
- [x] Add structured sequence checks missing from the direct module:
  `empty`, `count`, `contains`, `single`, `atMostOne`, `atLeastOne`, and `moreThanOne`.
- [x] Add option presence aliases:
  `Check.Option.present`, `Check.Option.empty`, `Check.Option.notEmpty`.
- [x] Add value-option presence aliases:
  `Check.ValueOption.present`, `Check.ValueOption.empty`, `Check.ValueOption.notEmpty`.
- [x] Add nullable presence aliases:
  `Check.Nullable.present`, `Check.Nullable.empty`, `Check.Nullable.notEmpty`.
- [ ] Keep `Check.Result.ok` and `Check.Result.error` direct-only for now; do not add top-level `Check.ok` or
  `Check.error` unless the constructor-like names prove useful enough.
- [ ] Add only a very small SRTP top-level facade at first:
  `Check.present`, `Check.empty`, and `Check.notEmpty`.
- [ ] Ensure SRTP facade functions only delegate to direct module implementations; do not make SRTP the semantic source
  of truth.
- [ ] Do not use SRTP for `distinct`, count checks, length checks, format checks, numeric ranges, result checks, or
  equality checks in the first pass.
- [ ] Move or remove top-level boolean predicates from `Check` so top-level `Check.*` consistently means structured
  `Result<unit, CheckFailure list>`.
- [ ] Add or relocate boolean predicates outside structured `Check.*`, using type-specific predicate modules or
  extensions such as `Seq.isDistinct`, `String.isBlank`, `Result.isOk`, and nullable/option presence helpers.
- [ ] Do not add `Seq.distinct` as a boolean predicate or structured check; it collides with FSharp.Core's sequence
  transformation.
- [ ] Decide null semantics before implementation:
  `Check.String.empty null`, `Check.Seq.empty null`, and whether `Check.Seq.notEmpty null` keeps current
  `Count(MinimumCount 1, None)` behavior.
- [ ] Update API shape tests so top-level `Check` contains structured check names, not the old predicate-only surface.
- [ ] Add behavior tests for both direct modules and top-level facade functions.
- [ ] Add tests proving `Check.present`, `Check.empty`, and `Check.notEmpty` work for string, option, value option,
  nullable, and sequence values where applicable.
- [ ] Add composition tests using tightened top-level checks, including function-list use such as
  `Check.all [ Check.present; Check.lengthBetween 2 40 ]`.
- [ ] Update source comments for the tightened `Check` model.
- [ ] Regenerate reference docs only after source comments and public APIs are updated.

## Phase 5: Continue Schema Core Constraints And Lowering

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
- [ ] Prove `Schema` can lower to a high-performance compiled record plan before codec work starts:
  - Reasoning: CodecMapper gets its JSON performance by compiling authored schemas into direct record codecs, not by
    interpreting a rich metadata tree for every value. Its hot path uses ordered field chains, cached field-name bytes,
    indexed field storage, typed field decoders, and constructor-specialized record decoders. Axial schema must preserve
    enough typed information for an equivalent lowering.
  - The proof should compile at least one flat record schema into a plan with ordered fields, cached UTF-8 external
    names, typed per-field decode/encode hooks, indexed field slots, and direct constructor application.
  - The plan must not require per-value runtime reflection, generic dictionary dispatch, or `obj array` constructor
    application on the hot path.
  - The authored schema path must be AOT- and trimming-safe: no runtime reflection as the foundation for constructor
    binding, field discovery, validation, or codec execution.
  - The authored schema path must remain Fable-compatible. Any .NET-only acceleration may use conditional compilation,
    but there must be a portable fallback that keeps the same explicit schema semantics.
  - Compare the intended lowering shape against `../../CodecMapper/main` before accepting the API. Use CodecMapper's
    benchmark scenarios as the performance reference once an Axial JSON codec prototype exists.
- [ ] Do not start RawInput, schema validation, rules, or DSL work until Phase 5 proves a vertical schema metadata slice:
  ordered fields, primitive value schema, at least required and maxLength metadata, lowering to `Check`, metadata
  inspection, constructor/getter alignment, and the compiled-record-plan proof above.

## Phase 6: Add Refined Value Schemas

- [ ] Define `Value.refined` or equivalent for named refined/domain types.
- [ ] Require both construction and inspection functions for refined value schemas.
- [ ] Support refined schemas over primitive schemas, especially text.
- [ ] Support `format` metadata such as `email`.
- [ ] Ensure refined value schemas can run `Check` programs.
- [ ] Ensure model schemas can use `field "email" _.Email Email.schema { required }`.
- [ ] Add examples for `Email`, `ContactName`, positive/non-negative numbers, and bounded strings.
- [x] Decide which refined schemas ship in `Axial.Refined` versus examples only: examples/user code only unless the
  package-boundary invariant changes.

## Phase 7: Build RawInput And Input Parsing

- [ ] Keep all schema input parsing source in `Axial.Validation.Schema`, not `Axial.Validation` or `Axial.Schema`.
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

## Phase 8: Define Schema Errors And Diagnostics Interpretation

- [ ] Keep `SchemaError` and schema diagnostics interpretation in `Axial.Validation.Schema`, not core `Axial.Schema`.
- [ ] Define `SchemaError`.
- [ ] Include required, expected scalar/object/many, invalid format, too short, too long, out of range, count failures,
  duplicate failures, constructor failures, and custom code/message.
- [ ] Map `CheckFailure` to `SchemaError`.
- [ ] Attach errors to `Diagnostics<'error>` paths.
- [ ] Keep field names out of `SchemaError` when diagnostics path already carries them.
- [ ] Support custom messages on schema constraints.
- [ ] Support mapping schema/input errors to domain/application errors.
- [ ] Add tests for path rendering and flattened diagnostics.

## Phase 9: Nested Models And Collections

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

## Phase 10: Constructor-Level Intrinsic Errors

- [ ] Support `schemaResult` or equivalent for constructors returning `Result`.
- [ ] Attach constructor errors at root by default.
- [ ] Support attaching constructor errors to a field path with `Input.constructorErrorAt "end"` or equivalent.
- [ ] Decide how constructor errors compose with field errors.
- [ ] Add `DateRange`-style tests for cross-field intrinsic invariants.
- [ ] Document the difference between constructor invariants and contextual rules.

## Phase 11: Validation Interpreter For Existing Models

- [ ] Keep schema-based validation interpreters in `Axial.Validation.Schema`, not core `Axial.Validation`.
- [ ] Implement `Validation.validate : Schema<'model> -> 'model -> Validation<'model, SchemaError>` or equivalent.
- [ ] Use getters, not raw input.
- [ ] Reuse schema constraints and `Check` lowering.
- [ ] Validate nested models through nested schemas.
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
