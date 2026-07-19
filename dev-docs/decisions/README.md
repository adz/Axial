# Decision Summary

This folder keeps only high-level durable decisions. Detailed historical specs are deleted once their useful rules have
been folded into `AGENTS.md`, `dev-docs/PLAN.md`, or this summary.

## 2026-07-16: HTTP hosts lower schema-trusted endpoint Flows without owning routing

- `Axial.Schema.Http.AspNetCore` and `.GenHttp` depend on both `Axial.Schema.Http` and `Axial.Flow`. Their default
  endpoint API is an ordinary `flow { }`: `Request.json`/`form`/`query` establish trusted input,
  `EndpointFlow.run` embeds an HTTP-independent application workflow by projecting the explicit application
  environment, and `Response` constructs the successful native response plan.
- `flowEndpoint` is the host execution boundary. It constructs `HttpEndpointEnv<'app>` per request, propagates host
  cancellation where available, renders invalid schema input as RFC 9457 problem details, maps expected application
  failures through a supplied renderer, and preserves interruption and defects. ASP.NET request-scoped DI may be
  used only inside the environment factory; application workflows continue to receive typed environments.
- The server still owns route registration, middleware, authorization, and endpoint metadata. Axial produces the
  native handler passed to `MapPost` or GenHTTP `Post`; it does not introduce a cross-host router. The lower-level
  `SchemaRequest`/`SchemaResult` and `SchemaResponse` primitives remain for endpoints that need `RetainedParseResult` or
  other host-specific boundary control.

## 2026-07-17: Records are the primary wire-tier declaration; .contract is parked

- `[<DeriveSchema>]`-marked plain F# records are the primary way to declare the wire tier. The generator derives
  the permissive schema from the record through an FCS syntax-only frontend into the same AST, resolver, and
  emitter as `.contract` files, emitting a schema module only — the F# compiler catches record/schema drift.
- `.contract` files stay shipped and compiling but receive no further investment: no LSP, no new grammar
  features, docs lead with records. The bespoke-LSP plan is superseded — records get the entire F# IDE experience
  (highlighting, rename, find-references, hover) for free, which was most of what the LSP would have built.
- Whether `.contract` is removed from the public surface before 1.0 is decided by the config-system dogfood: if
  records cover it, the grammar goes, and the pipeline it funded remains as the record frontend's machinery.

## 2026-07-17: Two schema tiers — permissive wire DTOs, strict hand-written domain

- The boundary story has two schemas. A **wire schema** is shaped per format and permissive: it accepts what the
  format allows with light constraints, and its result is a plain public DTO record. A **domain schema** is strict
  hand-written F# — invariants, smart constructors, DUs. The wire result maps to the domain through an ordinary
  function returning `Result`; that mapping function is where strictness lives.
- Versioning applies to the wire tier. When stored payloads must keep parsing after the wire changes (events,
  messages, config files), the `Contract` engine chains frozen wire versions with typed migrations; the domain map
  runs after the chain.
- Contracts (`.contract` + schemagen) generate the wire tier concisely — record, schema, version-chain wiring.
  They are never the domain authoring surface. Making contracts universal (generated types blended with user code,
  domain-tier declarations) was explored and rejected: IDL-first domain modeling is a pattern the .NET ecosystem
  has consistently abandoned in favor of IDL-at-the-edge (protobuf, TypeSpec), F# adopters chose F#'s type
  language, and universal scope turns every F# type feature and every serialization format's semantics into a
  grammar feature request. Multi-format serialization (MessagePack, protobuf) would enter as additional Schema
  interpreters beside `Json.compile`, never as grammar features.
- Record → schema generation (deriving a permissive wire schema from a hand-written DTO record) is under
  consideration as the low-ceremony wire-tier entry point; `.contract` remains the at-scale entry point. Both
  produce the same kind of wire schema. The record frontend and contract frontend share the same resolver and emitter.
- User docs teach this order: wire/domain split and the mapping function first, versioning when compatibility
  enters, then contracts as the concise way to generate what was just set up by hand.

## 2026-07-16: schemagen generates version chains; migrations are builder parameters

- A `.contract` file may declare several versions of one contract, oldest first with no gaps, all in one file.
  The resolver enforces contiguity, single-file chains, and that superseded generated names (`ConfigV1`) do not
  collide with declared contracts.
- The latest version keeps the bare generated type and module name; superseded versions emit version-suffixed
  frozen types, schemas, parse/validate, and field references. References pin any declared version and lower to
  the corresponding generated name.
- The latest module gains a generated `contract` builder whose parameters are each typed n-1 → n migration plus
  the `VersionSource`, wiring `Contract.create`/`supersedes`/`build`. The grammar never names F# symbols for
  migrations; they stay hand-written functions the compiler checks against the generated version types, and
  cutting a new version breaks every construction site until its migration exists.
- The earlier gate ("multi-version schemagen only after dogfooding a hand-written chain") was resolved by keeping
  the engine unchanged and the generated surface minimal: the builder function is the only new emission, and the
  golden corpus (`profile.contract`) plus behavior tests exercise the wired chain end to end.
- User-facing documentation is `docs/schema/contracts.md`: the versioning model, hand-written `Contract`
  declaration, grammar by example, `schemagen` with `--check`, multi-version generation, and the wire-tier-only
  non-goals. Contracts stay positioned as the at-scale tier, never the entry point.

## 2026-07-15: App owns portable root lifetime; hosts adapt native lifecycle

- `App` in `Axial.Flow` is the portable application launcher. `App.run` runs a finite root Flow;
  `App.start` returns one `AppHandle<'error,'value>` with `Status`, shared `Completion`, and idempotent `Stop()`.
  Completion is published only after the root Flow scope has closed. Direct `ToTask`/`ToAsync` execution remains the
  interface for individual operations and interop boundaries.
- An application is still an ordinary Flow value. Its live `Layer` is composed with `Flow.provide` before launch;
  there is no `IApp` inheritance model, hidden environment, universal error renderer, or ambient application registry.
- Host adapters translate only native lifecycle and outcomes. `Axial.Flow.Hosting` supplies standalone .NET Ctrl+C /
  exit-code integration, Microsoft Generic Host lifetime, the MEL `ILog` adapter, and fiber logging.
  `Axial.Flow.Hosting.Node` supplies Node arguments, `process.env`, SIGINT/SIGTERM, and `process.exitCode`.
  `Axial.Flow.Hosting.Browser` supplies explicit UI ownership and structural `AbortSignal` integration; it does not
  treat visibility or unload events as dependable shutdown.
- Node and browser packages are JavaScript-only Fable bindings. Their .NET target asset exists because Fable consumes
  F# projects through MSBuild; entry points touch a native runtime guard immediately and fail loudly on .NET or the
  wrong JavaScript runtime rather than providing inert implementations.
- The earlier `LiveClock`, partial `Hosting.createBaseRuntime`, and environment-only `Startup.validateEnvironment`
  helpers were deleted. `Clock.live` and `BaseRuntime.fromServiceProvider` remain the single clock and provider-backed
  provisioning paths; application startup failures retain their typed cause until the host edge.

## 2026-07-14: Telemetry is runtime instrumentation, not a service contract

- There is no telemetry service package and no `IHas<...>` telemetry contract. Tracing, annotations, and fiber
  observability stay runtime instrumentation in `Axial.Flow.Telemetry`: `Activity.trace`/`Activity.traceWith` over
  the static `ActivitySource("Axial.Flow")`, annotation sinks via `Flow.addAnnotationSink`, and
  `FiberTelemetry.observe`/`observeWithSpans` installed through `Flow.withFiberObserver`.
- The rule "operational services are explicit services, not runtime slots" does not extend to telemetry, for two
  structural reasons. `ActivitySource`/`ActivityListener` is .NET's own ambient instrumentation model — hosts and
  test code subscribe with listeners, so a service indirection would add ceremony without adding substitutability.
  And fiber observers deliberately never see the environment (a forked fiber's observer outlives any one
  environment), so a service-shaped telemetry contract could not be applied where much of the instrumentation runs.
- Environment still participates declaratively, not as a service: `Activity.trace` reads the
  `IHasRequestId`/`IHasCorrelationId`/`IHasTenantId` trio and the extensible `IHasTelemetryTags` trait from `'env`
  and stamps them as span tags.
- Logging is the opposite case and stays an explicit service: `ILog` (with the MEL bridge in `Axial.Flow.Hosting`
  and `FiberLogging.observe`) is a substitutable application dependency, not host instrumentation.
- On Fable JavaScript targets the same decision holds with OpenTelemetry JS in the `ActivitySource` role:
  `Axial.Flow.Telemetry.JavaScript` ships `Otel.trace`/`Otel.traceWith` and `FiberTelemetry`
  observers with the .NET tag vocabulary, emitting through a host-supplied `@opentelemetry/api` object
  (`Otel.install`) via structural bindings — the package has no npm dependency, and the SDK, exporter, and
  context manager stay the application's concern. The .NET build of that package is inert (`install` throws,
  `trace` passes through). Two structural consequences are accepted and documented: environment traits are
  read structurally (interface type tests are erased in JS), and runtime-context fidelity under Fable is
  construction-time, so the trace combinator captures fiber id, annotations, and the composed annotation sink
  at invoke time rather than reading them inside the running async.

## 2026-07-13: Contract parsing preserves trust and diagnostics

- `Contract<'model>` is the wire-version engine: it selects an explicitly declared version, parses against that
  frozen schema, composes typed contiguous n-1 → n migrations, and reconstructs against the head schema.
- `Contract.parse` and `Contract.parseVersion` return the ordinary `'model` in `Result<'model, ContractError>`.
  A successful contract parse has passed the head schema's field and constructor gates.
- `ContractError.ParseFailed` and `MigrationError.RevalidationFailed` carry `Diagnostics<SchemaError>`. The earlier
  sketch used one `SchemaError`, but `Schema.parse` and `Schema.check` can report several path-bearing failures;
  selecting one would discard boundary information.
- Version labels are positive and contiguous. `supersedes` registers only the immediately preceding version, matching
  the promised n-1 → n migration model and preventing accidental gaps in a chain.
- Multi-version `schemagen` output is not part of the engine. It follows only after a real hand-written version chain
  has been dogfooded.

## 2026-07-13: Recursive schemas use one memoized deferred model node

- `Schema.defer : (unit -> Schema<'model>) -> Schema<'model>` is the recursion primitive. Its thunk is memoized;
  parsing and reconstruction force it at each finite data node, while codec compilation installs a delayed plan so
  compiling a cyclic schema graph terminates.
- Inspection assigns traversal-local integer identities. The first occurrence is `SchemaShape.Deferred(id, value)`;
  an edge back to a value currently being expanded is `SchemaShape.Recursive id`. The public inspection tree therefore
  remains finite without runtime reflection or global identity state.
- JSON Schema lowers deferred identities to deterministic `recursiveN` entries in `$defs` and every recursive edge to
  `$ref`. Non-recursive schemas retain their previous inlined output.
- `schemagen` permits a contract to reference its own pinned version and emits `Schema.defer`; references to later,
  different contracts still violate declaration order. Internally-tagged union payloads remain immediate nested model
  schemas because their fields must be known while validating discriminator collisions; recursive models can contain
  unions, but an inline-union case cannot itself be the deferred edge.
- Recursive authoring uses a delayed schema holder so the thunk returns the same built schema. Calling a schema
  factory afresh from the thunk would create an endless sequence of distinct deferred nodes and defeat cycle identity.

## 2026-07-13: Schema test data is a non-packable FsCheck adapter over Data

- The repository contains no property-test dependency from the adoption target, so there was no evidence for its
  actual choice. The first adapter uses FsCheck 3.3.3 because this repository already uses xUnit and FsCheck has the
  established F# generator API and xUnit integration. This is not a core dependency or a commitment against a later
  Hedgehog adapter.
- `Axial.Schema.Testing` is non-packable and references `Axial.Schema` plus FsCheck. `Axial.Schema` remains
  dependency-free from test frameworks.
- Generation produces constraint-satisfying `Data` from `Inspect` metadata. `SchemaGen.model` then parses it and
  filters constructor rejections, so successful samples are schema-checked `'model` values and constructor
  invariants are not duplicated in the generator.
- Built-in lowering covers primitives, refined representations, nested models, collections and maps with count
  bounds, options, all three union forms, recursive references, email/length/choice constraints, ordered numeric
  bounds, and numeric multiples. FsCheck's size controls recursive depth; a zero-size collection becomes empty when
  its minimum count permits that finite base case.
- Pattern reversal, custom constraints, `notEqualTo`, `contains`, and `distinct` are not guessed. Derivation returns
  `UnsupportedConstraint(path, code)` unless `SchemaGen.rawWith` supplies a generator for that exact field path.

## 2026-07-13: FieldRef is an immutable-record lens

- `FieldRef<'model,'value>` carries the external `Name`, typed `Get`, and typed `Set` functions. `Set model value`
  returns a model copy with only that field replaced; it does not validate or mutate the input.
- Generated field references use F# record-copy expressions. This keeps updates reflection-free, preserves unrelated
  fields, and makes the same reference useful for diagnostics, forms, draft editing, and future patch application.
- Setters operate on draft model values. Code that requires schema trust must pass the updated draft through
  `Schema.check`; a field setter does not imply that cross-field constructor invariants still hold.

## Current Invariants

- `Flow<'env, 'error, 'value>` is the public workflow model. Platform carriers are execution/adaptation boundaries, not
  user-facing workflow types.
- There are two leaf packages: `Axial.Flow` and `Axial.ErrorHandling`. `Axial.ErrorHandling` has no internal Axial
  dependencies and hosts three namespaces — `Axial.ErrorHandling` (`Check`, `Predicate`, `Result`), `Axial.Validation`
  (accumulating diagnostics), and `Axial.Refined` (single-value parsing and refinement) — because none of the three
  depend on Schema or Flow and all three are single-value/error-vocabulary concerns, not model-declaration concerns.
  `Axial.Schema` legitimately depends on `Axial.ErrorHandling` (for `Check`-based constraint lowering and the
  `RefinedSchema` bridge into `Axial.Refined`); `Axial.Codec` depends on `Axial.Schema`. `Axial.Flow` stays
  independent of both. The `leaf packages stay independent of each other` API-shape test enforces this graph, and
  `` `Axial.Refined` was moved from `Axial.Schema` into `Axial.ErrorHandling` `` after finding it has zero actual
  dependency on Schema — see the ApiShapeTests.fs comments
  for the reasoning.
- Explicit dependencies live in `'env`. The ambient runtime is reserved for closed executor mechanics such as
  cancellation, scope, scheduling, interruption, and trace metadata.
- Operational services are explicit services provisioned through records, nominal `IHas<'service>` contracts, host-edge
  `IServiceProvider` resolution, and `Layer`.
- Operational service contracts do not live in `Axial.Flow`. Clock, log, random, GUID, and environment-variable
  contracts and operations belong to the optional `Axial.Flow.PlatformService` package. Its internal `Platform` file
  is the only place target-specific implementations may use `FABLE_COMPILER`; the public operation layer stays
  portable and host-specific capabilities such as process environment access are injected at the boundary.
- `Check` and `Result` helpers belong to the `Axial.ErrorHandling` namespace; `Parse`, `Refine`, and the `refine { }`
  builder belong to `Axial.Refined`; `Validation` and `Diagnostics` belong to `Axial.Validation`; `Policy`, `Bind`,
  and `BindError` belong to `Axial.Flow`. All of the first three ship in the `Axial.ErrorHandling` package.
- `Check` is a complete typed value-constraint subsystem:
  `Check<'value> = 'value -> Result<'value, CheckFailure list>`. Checks are path-free, raw-input-free value programs;
  value-preserving guards and extraction helpers belong in `Result`, and parsing and refined value construction belong in
  `Axial.Refined`. `Result` itself stays generic `Option`/`seq`/nullable → `Result` plumbing (`someOr`, `headOr`, etc.)
  — it must not grow a predicate- or domain-specific helper when the same rule already is, or should be, a named type
  in `Axial.Refined`'s catalog (`NonBlankString`, `Slug`, `PositiveInt`, ...); that catalog is the "reusable named
  proof" tier, `Result` is the "generic container extraction" tier, and the two must not blur together.
- Built-in refined schema helpers live in `Axial.Schema.RefinedSchema`, not `Axial.Refined`, so the refined
  namespace stays independent of schema metadata even though both now ship in the same package. Standalone refined
  constructors continue to use executable `Check` programs; the integration catalog mirrors those same constraints as
  `SchemaConstraint` metadata and tests the lowered boundary failures. Do not move `SchemaConstraint` into
  `Axial.Refined` or add an extra shared metadata package unless a second integration package needs that abstraction.
- `Result` keeps fail-fast adapters around `Check`, not a second accumulating constraint language. The current
  surface (`src/Axial.ErrorHandling/Result.fs`) is: generic combinators and conversions (`ok`, `error`, `map`,
  `mapError`, `bind`, `orElse`, `orElseWith`, `requireTrue`, `okIf`, `failIf`, `orError`, `fromTry`, `fromChoice`,
  `toOption`, `toValueOption`, `defaultValue`) and extraction helpers for option, value option, nullable, result, and
  sequence values (`someOr`, `noneOr`, `valueSomeOr`, `valueNoneOr`, `nullableOr`, `notNullOr`, `okOr`, `errorOr`,
  `headOr`). No value-preserving fail-fast guard family (`keepIf`/`Result.require`/`Result.guard`, string length,
  ordered range, sequence count) has actually been added — an earlier version of this doc described that family as
  already retained; it was aspirational, not built. If it's added later, the same don't-duplicate-`Refined` rule
  above applies: a guard that proves a value satisfies a rule, rather than merely converting a container, belongs in
  `Axial.Refined`'s catalog, not `Result`. Do not add new predicate-specific `Result` helpers when the same rule
  belongs in `Check.*` or `Axial.Refined` instead.
- First-pass ordered range checks stay in generic `Check.Number` helpers over comparable values. Do not add separate
  `Check.Int`, `Check.Decimal`, `Check.Float`, or date/time check modules until a schema, refined value, or diagnostics
  requirement needs type-specific semantics beyond plain ordering.
- `Axial.Schema` starts as its own package and project as soon as schema source work begins. Do not incubate schema
  definitions inside `Axial.Validation`; keep schema definitions independent and put input, validation, diagnostics, and
  rules integration in `Axial.Schema`.
- Constructor-last object shapes are the sole public schema-authoring surface:
  `Schema.define<Customer> |> field "name" _.Name |> ... |> construct ctor`. The shape phantom records field types;
  `construct` and `constructResult` check the closing constructor by position and arity. Completed schemas retain a
  typed record plan beside erased metadata so codecs apply constructors directly without `obj array` dispatch.
- Primitive value schemas use the primitive names directly: `Schema.text`, `Schema.int`, `Schema.decimal`,
  `Schema.bool`, `Schema.date`, `Schema.dateTime`, and `Schema.guid`. They are `Schema<'value>` values supplied as
  the explicit argument to `fieldWith Schema.text "name" _.Name`, alongside composites (`Schema.list`,
  `Schema.option`, `Schema.map`, `Schema.union`, `Schema.inlineUnion`, `Schema.enum`, `Schema.defer`) and custom
  refined/domain schemas. Common primitive, option, and list fields use inferred `field`; other schemas use
  `fieldWith`. Do not add competing aliases such as `string`, `integer`, `boolean`, `uuid`, `dateOnly`, or `Field.text`;
  the `Value` module is internal and is not public vocabulary.
- Non-validation interpreters start from the public `Inspect` API (`Inspect.model`, `Inspect.schema`, `Inspect.field`),
  which describes a built schema as plain metadata trees (`ModelDescription`, `FieldDescription`, `SchemaDescription`,
  `SchemaShape`). Inspection never parses input, runs checks, or constructs models. JSON Schema, documentation, and UI
  metadata generators are prototype interpreters over that read model, not core packages, until a consumer demands one.
- CodecMapper-style codecs consume schema by referencing `Axial.Schema` only, in their own package: metadata comes from
  `Inspect`, and hot-path plans come from the record-plan compiler protocol that walks typed fields and a typed
  constructor finalizer. `Axial.Schema` never references a codec
  package, and codec packages never reference `Axial.Schema`, so no dependency cycle can form.
- `Axial.Schema.Syntax` contains only the constructor-last shape operations and typed field constraints intended for a
  local declaration-module `open`. Primitive and composite value-schema functions remain qualified under `Schema`.
- Build-time generation emits the same constructor-last syntax as handwritten schemas. Reflection remains rejected as
  a schema foundation.
- `Bind` is only for assigning or mapping a source error immediately before `flow { }` binds it. In pure code, use
  `Result.mapError` or `Validation.mapError`.
- Generated reference docs come from XML comments and generator inputs. Do not hand-edit generated reference pages as the
  primary source of truth.
- Compiled JSON codecs live in `Axial.Codec`, a package that references only `Axial.Schema` (through
  `InternalsVisibleTo` for the type-erased definitions) and mirrors CodecMapper's byte-level runtime. The codec is the
  trusted hot path: it enforces wire shape and required fields but does not run constraint metadata. Untrusted boundary
  input keeps going through `Data` + `Schema.parse` for complete path-aware diagnostics. Do not fold codecs into
  `Axial.Schema`: codecs must not pull diagnostics into the schema package, and the schema core stays free of any
  wire runtime.
- A `dotnet new axial-api` template is evaluated and deferred until the public surface stabilizes (at or near 1.0).
  The seed exists as `examples/Axial.Api`, which CI smoke-runs on every push, so the template would only add packaging
  around a sample that still changes with the pre-1.0 API. Revisit when (a) the schema/codec/boundary surface has been
  stable for two consecutive releases, and (b) at least one external user asks for a scaffold; then package the sample
  as a template repo folder with `dotnet new` metadata rather than a separate NuGet-first workflow.

- Codec decode allocation work (beating STJ the way CodecMapper does) is deferred until performance becomes a pitch
  line; parity on speed with the 6x boundary-lane gap is the current story. If pursued, the pre-chosen approach is
  fixed-arity typed decoders for arities 1..8 with the slot decoder as fallback — no reflection, dispatch on field
  count from the compiled record plan in `Schema.compilePlan` — with a target of ≤ 2.0 µs / ≤ 1.5 KB on the benchmark aggregate.
- There is no "checked codec" compile option. `Axial.Codec` enforces wire shape only; a consumer who wants constraint
  enforcement on trusted-lane decode composes `Json.deserialize` then `Schema.check` (one extra model walk). If
  that composition proves too slow for a real consumer, the pre-chosen answer is a `Json.deserializeValidated` helper
  in `Axial.Schema` (interpreters may reference Codec, never the reverse). Duplicating constraint lowering
  inside `Axial.Codec` stays rejected.
- Unions support three wire shapes: the externally-wrapped `{discriminator, payload}` object (`Schema.union`, the
  default), internally-tagged objects (`Schema.inlineUnion` — valid only when every payload is an object whose field
  names don't collide with the discriminator, checked at construction), and bare-string enums (`Schema.enum`) for
  payload-less cases. All three are implemented across Schema.parse, Codec, JsonSchema, and Inspect; the contract
  grammar's literal unions (`"a" | "b"`) lower to `Schema.enum`. No untagged unions — discriminators are required.
- `JsonSchema.generate`/`generateValue` pin `$schema` to draft 2020-12 and carry description metadata
  (`Schema.describe`) into `description` (field/value level) and `title` (model root). `$defs`
  hoisting for non-recursive nested reuse is deferred until a sample has real need; recursive schemas
  (`Schema.defer`) lower to deterministic `recursiveN` entries in `$defs` with `$ref` edges, so inlining terminates.
- The UI-metadata interpreter stays a prototype. Promotion waits for an external consumer; if promoted, the API sample
  must consume the shipped module, otherwise the duplication just moves. UI scope stays field list + control kinds —
  layout, localization, and widget options are application concerns.
- `Axial.Codec` is part of the supported Fable surface: the package compiles in `check-fable-js-surface.sh` and a Node
  round-trip test exercises it. The `FABLE_COMPILER` gates are load-bearing, and every future codec optimization must
  keep the JS branch working. This completes the zod-comparison story — one declaration shared between server and
  browser covers serialization as well as parsing.
- No fused fast boundary path for now: the 20 µs boundary-lane cost is not a reported problem, and `Schema.parse` keeps
  its raw-retaining redisplay contract. If demand appears, the pre-chosen shape is a separate entry point
  (`Schema.parseUtf8` — diagnostics-on-failure, no redisplay, API bodies), prototyped in the benchmarks project first,
  exactly how the codec earned promotion. Never an optimization flag on `Schema.parse`.
- `Data.ofJsonElement`/`ofJsonDocument` stay gated to `net8.0 && !FABLE_COMPILER`. If a netstandard2.1 consumer
  ever asks, the pre-chosen answer is a TFM-conditional `System.Text.Json` package reference on netstandard2.1 only —
  not a split adapter package, which would force a different module name.
- The `Schema` module starts declarations with `Schema.define`; `Axial.Schema.Syntax` provides fields and closing
  constructors. `Schema` also hosts the model
  operations that use a schema as authority: `Schema.parse` / `Schema.parseWith` / `Schema.parseWithOptions`
  (untyped `Data` → `RetainedParseResult<'model, SchemaError>`) and `Schema.check` (an already-existing model value,
  re-checked through its field constraints and its constructor so cross-field invariants aren't silently skipped).
  There is no separate public `Model` module.
- `Schema.check` replaced the old `Axial.Schema.Validation.validate`, which only re-checked per-field constraints
  and silently skipped the model's own constructor invariant (a `DateRange` with `Start` after `End` would have
  passed it). `Schema.check` re-runs field constraints and then re-invokes the constructor over the field getters'
  outputs specifically so the constructor re-check isn't a bolt-on special case.
- `RuleSet<'model,'error>`/`Rules` (contextual, workflow-dependent rules over an already-trusted model) is a known
  unresolved design problem, not a settled API — see the Open Ideas pointer below before extending it.
- `Model.construct` (typed field values in, schema-checked model out, without going through `Schema.parse`'s
  untyped `Data`) does not exist as a library function and cannot be added as one without either breaking the
  zero-reflection/AOT/Fable rule or capping arity with numbered overloads — see the Open Ideas pointer below. Do not
  attempt to add `Model.construct schema arg1 arg2 ...` as a plain function; the type-erasure wall is structural, not
  a missing-effort gap.

## Open Ideas

Pre-ideas and proposals live in [`../current-ideas/`](../current-ideas/). When accepted, keep only the durable rule here
or in `AGENTS.md`, then delete the detailed sketch.

- **`Model.construct`.** RESOLVED by reduction: there is no positional construction API or universal trust wrapper.
  Public wire/draft records use ordinary named-field construction followed by `Schema.check`; private domain types use
  their own smart constructors. The structural reason: `Schema<'model>` can't carry per-field types, so a typed
  positional `Model.construct` is impossible without source generation; every
  runtime shape tried (builder ceremony, tuple-returning `buildWithConstruct`, reflection off a draft record, a
  `(string * obj) list`) was rejected.
- **`Trusted<'model>`.** REJECTED after reference-app re-review. A universal wrapper made parse, contract, and ordinary
  domain construction carry library proof ceremony without establishing durable F# invariants. `Schema.check` returns
  the ordinary checked value for boundary admission; private representations and smart constructors provide durable
  guarantees where required. See `docs/schema/trusted-construction.md`.
- **`RuleSet`/`Rules`.** RESOLVED by reduction (2026-07-12): renamed to `ContextRules`, the `RuleSet` container
  type deleted. A rule is a plain `'model -> Result<unit, Diagnostics<'error>>`; a rule set is a plain list;
  context selection is the caller's own `match`/`Map`. `ContextRules` keeps only failure constructors
  (`fail`/`failAt`/`failAtField`/`custom`/`failCustom`), path scoping (`at`/`atField`/`name`/`key`/`index` —
  prefer `atField` over `name` so wire names can't drift), and `apply` over lists.
- **SRTP common names for `Check`.** RESOLVED (2026-07-14): the current hybrid is the decided surface. Top-level
  type-directed `Check.present`/`Check.empty`/`Check.notEmpty` exist (Check.fs `Present`/`Empty`/`NotEmpty`
  dispatch types) over `string`/`option`/`voption`/`Nullable`/`list`/`array`, for direct application to a value.
  The nested modules (`Check.String.present`, `Check.Option.present`, ...) remain the authoritative catalog —
  the browsable per-type surface and the disambiguation tier. The `inline` type-directed names also work as
  `Check<'value>` list elements once the element type is concrete (`Check.all [ Check.present; ... ]` is
  tested in CheckResultTests.fs). Do not
  extend type-directed dispatch beyond the presence/emptiness trio — other shared names (`some`, `ok`, numeric
  comparisons) are either container-specific or already generic without SRTP dispatch.
- **Refined guide docs area.** `Axial.Refined`'s API reference now lives under `/error-handling/reference/refined/`
  (it moved with the package), but the hand-written guide pages (`docs/schema/refined/*.md`) still live under the
  `/schema/` docs area for now. Whether to move the guides too is an open site-IA question, not decided either way.

- Construction has two deliberate strengths. Public wire/draft records may be assembled with named fields and admitted
  through `Schema.check`; this is a successful-flow guarantee, not a new type-level proof. Domain types that require a
  durable guarantee use private representations and authoritative smart constructors, with `Schema.refine` or a record
  schema invoking those same constructors. A separate draft is useful for named assembly/editing of a private
  cross-field aggregate, not as a universal wrapper pattern. See `docs/schema/trusted-construction.md`.
- The contract grammar/generator (`src/Axial.Schema.Contracts`, `scripts/schemagen`) is WIRE-tier tooling only.
  Domain models are hand-written F#; a domain-tier declaration kind was designed and rejected (generated types
  can't carry methods; DUs don't fit a JSON-shaped grammar). Golden corpus: `tests/Axial.Schema.Tests/contracts/`
  (compiled + behavior-tested) with byte-for-byte emission tests in `tests/Axial.Schema.Contracts.Tests`.
