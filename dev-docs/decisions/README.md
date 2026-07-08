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
  evaluated (see `schema-ce-evaluation.md` in this folder): the sketched bare-brace constraint blocks are not
  expressible in F#, compile-error quality is a wash, and readability does not improve, so the pipeline builder stays
  the single public authoring surface. `Axial.Schema.DSL` later delivered the CE's prefix-elimination motivation as a
  curated open module over the same pipeline, further closing the case for a second authoring surface.
- `Axial.Schema.DSL` is the one non-`RequireQualifiedAccess` schema module, designed to be opened inside a schema
  definition module only. Field combinators take the constraint list first with `[]` for unconstrained fields;
  `int`/`decimal`/`bool` deliberately shadow the core conversion functions within that scope. Do not add bare-name
  aliases to other modules; curation lives in `DSL` alone.
- Source generation is deferred and reflection stays rejected as a schema foundation. The `[<Schema>]` generation
  target is pinned by `dev-docs/current-ideas/schema-source-generation.md` and compiled by
  `tests/Axial.Schema.Tests/SchemaGenerationTargetProofTests.fs`; generated schemas may target public or `internal`
  record representations, but not `private` ones, because F# has no partial types for same-scope emission.
- `Bind` is only for assigning or mapping a source error immediately before `flow { }` binds it. In pure code, use
  `Result.require`, `Result.mapError`, or `Validation.mapError`.
- Generated reference docs come from XML comments and generator inputs. Do not hand-edit generated reference pages as the
  primary source of truth.
- Compiled JSON codecs live in `Axial.Codec`, a package that references only `Axial.Schema` (through
  `InternalsVisibleTo` for the type-erased definitions) and mirrors CodecMapper's byte-level runtime. The codec is the
  trusted hot path: it enforces wire shape and required fields but does not run constraint metadata. Untrusted boundary
  input keeps going through `RawInput` + `Input.parse` for complete path-aware diagnostics. Do not fold codecs into
  `Axial.Validation.Schema` (they must not pull in diagnostics) or into `Axial.Schema` (the schema core stays free of
  any wire runtime).
- A `dotnet new axial-api` template is evaluated and deferred until the public surface stabilizes (at or near 1.0).
  The seed exists as `examples/Axial.Api`, which CI smoke-runs on every push, so the template would only add packaging
  around a sample that still changes with the pre-1.0 API. Revisit when (a) the schema/codec/boundary surface has been
  stable for two consecutive releases, and (b) at least one external user asks for a scaffold; then package the sample
  as a template repo folder with `dotnet new` metadata rather than a separate NuGet-first workflow.

- Codec decode allocation work (beating STJ the way CodecMapper does) is deferred until performance becomes a pitch
  line; parity on speed with the 6x boundary-lane gap is the current story. If pursued, the pre-chosen approach is
  fixed-arity typed decoders for arities 1..8 with the slot decoder as fallback — no reflection, dispatch on field
  count from the typed chain in `Schema.specialize` — with a target of ≤ 2.0 µs / ≤ 1.5 KB on the benchmark aggregate.
- There is no "checked codec" compile option. `Axial.Codec` enforces wire shape only; a consumer who wants constraint
  enforcement on trusted-lane decode composes `Json.deserialize` then `Validation.validate` (one extra model walk). If
  that composition proves too slow for a real consumer, the pre-chosen answer is a `Json.deserializeValidated` helper
  in `Axial.Validation.Schema` (interpreters may reference Codec, never the reverse). Duplicating constraint lowering
  inside `Axial.Codec` stays rejected.
- Unions support three wire shapes: the externally-wrapped `{discriminator, payload}` object (`Value.union`, the
  default), internally-tagged objects (`Value.unionInline` — valid only when every payload is an object whose field
  names don't collide with the discriminator, checked at construction), and bare-string enums (`Value.enumOf`) for
  payload-less cases. All three are implemented across Input.parse, Codec, JsonSchema, and Inspect; the contract
  grammar's literal unions (`"a" | "b"`) lower to `Value.enumOf`. No untagged unions — discriminators are required.
- `JsonSchema.generate` pins `$schema` to draft 2020-12 and carries description metadata into `title`/`description`
  (both queued work). `$defs` hoisting is deferred until a sample has real nested reuse; recursion is not expressible
  in the builder today, so inlining cannot fail to terminate. Draft selection waits for a real consumer.
- The UI-metadata interpreter stays a prototype. Promotion waits for an external consumer; if promoted, the API sample
  must consume the shipped module, otherwise the duplication just moves. UI scope stays field list + control kinds —
  layout, localization, and widget options are application concerns.
- `Axial.Codec` is part of the supported Fable surface: the package compiles in `check-fable-js-surface.sh` and a Node
  round-trip test exercises it (queued work). The `FABLE_COMPILER` gates are load-bearing, and every future codec
  optimization must keep the JS branch working. This completes the zod-comparison story — one declaration shared
  between server and browser covers serialization as well as parsing.
- No fused fast boundary path for now: the 20 µs boundary-lane cost is not a reported problem, and `Input.parse` keeps
  its raw-retaining redisplay contract. If demand appears, the pre-chosen shape is a separate entry point
  (`Input.parseUtf8` — diagnostics-on-failure, no redisplay, API bodies), prototyped in the benchmarks project first,
  exactly how the codec earned promotion. Never an optimization flag on `Input.parse`.
- `RawInput.ofJsonElement`/`ofJsonDocument` stay gated to `net8.0 && !FABLE_COMPILER`. If a netstandard2.1 consumer
  ever asks, the pre-chosen answer is a TFM-conditional `System.Text.Json` package reference on netstandard2.1 only —
  not a split adapter package, which would force a different module name.

## Open Ideas

Pre-ideas and proposals live in [`../current-ideas/`](../current-ideas/). When accepted, keep only the durable rule here
or in `AGENTS.md`, then delete the detailed sketch.
