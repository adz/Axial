# Decision Summary

This folder keeps only high-level durable decisions. Detailed historical specs are deleted once their useful rules have
been folded into `AGENTS.md`, `dev-docs/PLAN.md`, or this summary.

## Current Invariants

- `Flow<'env, 'error, 'value>` is the public workflow model. Platform carriers are execution/adaptation boundaries, not
  user-facing workflow types.
- `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Schema`, and `Axial.Validation` are leaf packages. The
  umbrella `Axial` package and `Axial.Validation.Schema` may reference them, but leaf packages must not depend on each
  other, with one deliberate exception: `Axial.Refined` references `Axial.ErrorHandling` because `Check` appears in its
  public signatures. The `leaf packages stay independent of each other` API-shape test enforces this graph.
- Explicit dependencies live in `'env`. The ambient runtime is reserved for closed executor mechanics such as
  cancellation, scope, scheduling, interruption, and trace metadata.
- Operational services are explicit services provisioned through records, nominal `IHas<'service>` contracts, host-edge
  `IServiceProvider` resolution, and `Layer`.
- `Check` and `Result` helpers belong to `Axial.ErrorHandling`; `Parse`, `Refine`, and the `refine { }` builder belong
  to `Axial.Refined`; `Validation` and `Diagnostics` belong to `Axial.Validation`; `Policy`, `Bind`, and `BindError`
  belong to `Axial.Flow`.
- `Check` is a complete typed value-constraint subsystem:
  `Check<'value> = 'value -> Result<unit, CheckFailure list>`. Checks are path-free, raw-input-free value programs;
  value-preserving guards and extraction helpers belong in `Result`, and parsing and refined value construction belong in
  `Axial.Refined`.
- Built-in refined schema helpers live in `Axial.Validation.Schema.RefinedSchema`, not `Axial.Refined`, so the refined
  package stays independent of schema metadata. Standalone refined constructors continue to use executable `Check`
  programs; the integration catalog mirrors those same constraints as `SchemaConstraint` metadata and tests the lowered
  boundary failures. Do not move `SchemaConstraint` into `Axial.Refined` or add an extra shared metadata package unless a
  second integration package needs that abstraction.
- `Result` keeps fail-fast adapters around `Check`, not a second accumulating constraint language. The retained helper
  families are:
  - generic Result combinators and conversions (`ok`, `error`, `map`, `bind`, `mapError`, `withError`, `fromTry`,
    `fromChoice`, `toOption`, `toValueOption`, and `defaultValue`);
  - extraction helpers for option, value option, nullable, result, and sequence values (`someOr`, `noneOr`,
    `valueSomeOr`, `valueNoneOr`, `nullableOr`, `notNullOr`, `okOr`, `errorOr`, `headOr`, `single`, and `atMostOne`);
  - value-preserving fail-fast guards that mirror executable `Check` programs (`keepIf` today; `Result.require` and
    `Result.guard` when the API is aligned; string length, ordered range, and sequence count guards).
  Do not add new predicate-specific `Result` helpers when the same rule belongs in `Check.*` and can be adapted through
  the generic fail-fast guard.
- First-pass ordered range checks stay in generic `Check.Number` helpers over comparable values. Do not add separate
  `Check.Int`, `Check.Decimal`, `Check.Float`, or date/time check modules until a schema, refined value, or diagnostics
  requirement needs type-specific semantics beyond plain ordering.
- `Axial.Schema` starts as its own package and project as soon as schema source work begins. Do not incubate schema
  definitions inside `Axial.Validation`; keep schema definitions independent and put input, validation, diagnostics, and
  rules integration in `Axial.Validation.Schema`.
- The explicit schema core is a CodecMapper-style progressive typed builder:
  `Schema.recordFor<Customer, _> ctor |> Schema.field "name" _.Name Value.text |> ... |> Schema.build`.
  `Schema.recordFor<'model, _>` is the everyday entry point because it anchors the model type before the first field,
  allowing shorthand member getters. Plain `Schema.record ctor` remains available when the model type is already clear
  or getters are annotated explicitly. Each field application peels one curried constructor argument and `Schema.build`
  requires a fully applied constructor, so constructor/getter alignment is compiler-checked by argument position and
  authoring scales to any field count. The former `Schema.map2`/`Schema.map3` proof shape is not the public authoring
  direction, and Axial should not grow a hand-written `Schema.mapN` family. Do not route larger models through a
  required `schema create { }` computation expression or `[<Schema>]` source generator; both are optional sugar over the
  progressive builder. The built schema must keep its typed field chain reachable alongside the type-erased descriptor
  view so codec interpreters can compile constructor-specialized plans from a `Schema<'model>` value alone, without
  `obj array` constructor application.
- Primitive schema field shorthands use the primitive names directly: `text`, `int`, `decimal`, `bool`, `date`,
  `dateTime`, and `guid`. They are field-authoring operations with external name first and getter second, for example
  `Schema.text "name" _.Name` in the pipeline surface and `text "name" _.Name { ... }` inside the optional
  `schema create { }` computation expression. Generic `Schema.field "email" _.Email Email.schema` and
  `field "email" _.Email Email.schema { ... }` are reserved for explicit or custom `ValueSchema<'value>` values such as
  refined/domain schemas and advanced composition. Do not add competing aliases such as `string`, `integer`, `boolean`,
  `uuid`, `dateOnly`, or `Field.text`; `Value.*` remains the lower-level value-schema vocabulary.
- Non-validation interpreters start from the public `Inspect` API (`Inspect.model`, `Inspect.value`, `Inspect.field`),
  which describes a built schema as plain metadata trees (`ModelDescription`, `FieldDescription`, `ValueDescription`,
  `ValueShape`). Inspection never parses input, runs checks, or constructs models. JSON Schema, documentation, and UI
  metadata generators are prototype interpreters over that read model, not core packages, until a consumer demands one.
- CodecMapper-style codecs consume schema by referencing `Axial.Schema` only, in their own package: metadata comes from
  `Inspect`, and hot-path plans come from `Schema.specialize` with an `IFieldChainFactory<'model, 'result>` that walks
  the typed field chain to compile constructor-specialized record plans. `Axial.Schema` never references a codec
  package, and codec packages never reference `Axial.Validation.Schema`, so no dependency cycle can form.
- The `schema create { ... }` computation expression is not shipped. A prototype over the progressive builder was
  evaluated (see `dev-docs/current-ideas/schema-ce-evaluation.md`): the sketched bare-brace constraint blocks are not
  expressible in F#, compile-error quality is a wash, and readability does not improve, so the pipeline builder stays
  the single public authoring surface.
- Source generation is deferred and reflection stays rejected as a schema foundation. The `[<Schema>]` generation
  target is pinned by `dev-docs/current-ideas/schema-source-generation.md` and compiled by
  `tests/Axial.Schema.Tests/SchemaGenerationTargetProofTests.fs`; generated schemas may target public or `internal`
  record representations, but not `private` ones, because F# has no partial types for same-scope emission.
- `Bind` is only for assigning or mapping a source error immediately before `flow { }` binds it. In pure code, use
  `Result.require`, `Result.mapError`, or `Validation.mapError`.
- Generated reference docs come from XML comments and generator inputs. Do not hand-edit generated reference pages as the
  primary source of truth.

## Open Ideas

Pre-ideas and proposals live in [`../current-ideas/`](../current-ideas/). When accepted, keep only the durable rule here
or in `AGENTS.md`, then delete the detailed sketch.
