# Axial Plan

This file tracks current product and architecture direction.
High-level durable decisions live in `dev-docs/decisions/`.
Speculative sketches live in `dev-docs/current-ideas/`, but this file is the live direction.

## Release Strategy

Per `prd.md`: the boundary stack — the `Axial.ErrorHandling` package (hosting the `Axial.ErrorHandling`,
`Axial.Validation`, and `Axial.Refined` namespaces), `Axial.Schema`, and `Axial.Codec` — is the 1.0 gate, driven by
a real adoption target (a ~100-variant versioned config system). The Flow group's remaining pre-1.0 scope in
`LATER_TODO.md` is demand-driven — pulled forward when a concrete application needs it. The contract-declaration
thread originally sequenced versioning/migration machinery before the grammar; in practice the grammar and generator
shipped first (2026-07-12, single-version wire-tier scope — see `dev-docs/current-ideas/contract-grammar.md`), and
the versioning/migration engine shipped on 2026-07-13, followed by Schema-depth work, dogfood, then LSP.
The schema surface has been through heavy recent churn (`Model<'t>`, `ContextRules`, contracts) and should be treated
as settling rather than settled.

## Current Direction

Axial began as a Reader-Async-Result workflow monad in the ZIO tradition; the result side has since expanded into a
full parse-don't-validate toolkit. The library is therefore two main groups, and public positioning should always
present them as such:

- **Parse-don't-validate results**: `Schema<'model>` is the front door for domain models — input parsing, intrinsic
  validation, redisplay, contextual rules, and metadata interpreters all fall out of one declaration. Plain `Result`
  with a user-owned error DU is the blessed lane for simple code without domain models. `Check`, `Validation`,
  `Refined`, and interpreter error types are machinery behind those two doors, not peer entry points.
- **Effects in Flow**: the workflow group below. Useful with or without schemas, and never part of the entry price
  for the results group.

Within the effects group, Axial has one fully expanded workflow shape:

```fsharp
Flow<'env, 'error, 'value>
```

Shorter type aliases cover common channel combinations:

```fsharp
Flow<'value> = Flow<unit, Never, 'value>
Flow<'error, 'value> = Flow<unit, 'error, 'value>
EnvFlow<'env, 'value> = Flow<'env, Never, 'value>
ExnFlow<'value> = Flow<unit, exn, 'value>
ExnEnvFlow<'env, 'value> = Flow<'env, exn, 'value>
```

Teach from the smallest useful shape first, then expand to `Flow<'env, 'error, 'value>` when environment and typed
failure channels matter. Use `Never` for an error channel that cannot fail, and use the `Exn*` aliases only for
recoverable exception-channel interop.

The active direction splits concerns like this:

- explicit services and app/domain dependencies live in `'env`
- executor mechanics live in a closed ambient runtime
- data boundaries are described with portable schemas, reusable value checks, path-aware input/validation interpreters,
  contextual rules, and environment-aware policies

First-party service packages and standard operational services should be expressed as explicit services, not runtime
slots.

Axial's data-boundary direction splits concerns like this:

- `Check<'value>` describes reusable, path-free, raw-input-free value constraints
- `Schema<'value>` describes typed shape, construction, inspection, and portable constraint metadata; `Schema.parse`
  admits raw input and `Schema.check` rechecks an already assembled typed value through its field schemas and record
  constructor. Successful operations return the ordinary value rather than a universal trust wrapper.
- schema interpreters parse raw input, validate existing models, produce diagnostics, and drive non-validation metadata
  consumers
- contextual rules are plain functions over already-trusted models; `ContextRules` supplies only failure constructors,
  `FieldRef`-based path scoping, and `apply` — context selection is the caller's own `match`/`Map`
- policies adapt checks, parsers, validations, and rules into `Flow`

Core schema declarations and their interpreters share the single `Axial.Schema` namespace and package (module names,
not namespaces, separate declaration from interpretation); the package stays independent of flow execution.

Constructor-level intrinsic errors are a second stage after field parsing and field constraints, not an error source that
runs alongside invalid fields. If any field or nested item has intrinsic diagnostics, interpreters must not apply the
model constructor; constructor errors are reported only when every constructor argument is already trusted. By default
constructor errors attach to the current object path, and input parsing may expose an option to attach them to a relative
field path when that gives better boundary feedback.

Schema work should prove the portable metadata model before growing broad interpreters. The metadata slice — field
ordering, primitive value schemas, schema constraints as inspectable metadata, lowering those constraints to `Check`,
and constructor/getter alignment — is proven. The explicit core API is a CodecMapper-style progressive typed builder:

```fsharp
Schema.recordFor<Customer, _> ctor
|> Schema.field "id" _.Id Value.int
|> Schema.field "name" _.Name Value.text
|> Schema.build
```

`Schema.recordFor<'model, _>` is the everyday entry point because it anchors the model type before the first field, letting
field getters use shorthand member access. Plain `Schema.record ctor` remains available when the model type is already
clear or getters are annotated explicitly. Each field application peels one curried constructor argument and
`Schema.build` only type-checks when the constructor is fully applied, so constructor/getter alignment is
compiler-checked by argument position and authoring scales to any field count without a hand-written `mapN` family,
computation expression, or source generator. The earlier `Schema.map2`/`Schema.map3` API was only a transitional proof
of the metadata model. The `schema create { }` computation expression was evaluated and rejected (see
`dev-docs/decisions/README.md`); `Axial.Schema.DSL` delivers its prefix-elimination motivation as an open module over
the same pipeline. Build-time generation exists as wire-tier tooling only (`.contract` files →
`src/Axial.Schema.Contracts` + `scripts/schemagen`); domain-tier generation was designed and rejected. Raw input,
schema validation, rules, and DSL work should build on the explicit builder core rather than bypass it.

The public schema-authoring vocabulary should make primitive fields the short path and custom value schemas the explicit
path. The primitive field operations are `text`, `int`, `decimal`, `bool`, `date`, `dateTime`, and `guid`, using the same
external-name-first, getter-second order as `Schema.field`. In the pipeline surface they are qualified builder steps
such as `Schema.text "name" _.Name`, with unqualified equivalents available by opening `Axial.Schema.DSL` inside a
schema definition module. Reserve generic `Schema.field "email" _.Email Email.schema` for explicit or custom
`ValueSchema<'value>` values, including refined/domain schemas, nested schemas, and advanced composition. Do not introduce competing primitive aliases such as `string`, `integer`, `boolean`, `uuid`,
`dateOnly`, or `Field.text`. `Value.text`, `Value.int`, and the other `Value.*` primitives remain the lower-level
value-schema vocabulary used by generic fields and interpreters, not the everyday field-authoring names.

Schema must also preserve a high-performance codec lowering path. The inspectable schema model may contain rich metadata,
but JSON codecs should not interpret that metadata tree directly on the hot path. A codec interpreter must be able to
compile schemas into direct record plans: ordered field descriptors, cached wire-name bytes, indexed field slots,
typed field decoders, and constructor application that does not require per-value reflection or `obj array` dispatch.
CodecMapper is the performance reference for this shape. This path now ships as `Axial.Codec` (`Json.compile` over the
retained typed field chain, benchmarked against `System.Text.Json` in `benchmarks/Axial.Benchmarks/CodecSuites.fs`);
remaining codec work is optimization and format breadth, not proving the shape.

The built `Schema<'model>` value itself must retain typed constructor and field information sufficient for that codec
specialization: type erasure at authoring time must not force interpreters onto boxed `obj array` dispatch or require
callers to re-supply the constructor and typed fields alongside the schema. The typed field chain that powers the
authoring builder is the same structure codec compilers walk to emit constructor-specialized plans (CodecMapper's
`MappingDefinition` / `Specialize` dual-view pattern).

Runtime reflection must not be the foundation for schema construction, constructor binding, validation, or codec
execution. Reflection can be an optional import/tooling path on .NET, but the core authored schema path must remain AOT-
and trimming-safe and must have a Fable-compatible fallback. If ergonomic boilerplate becomes painful, prefer build-time
generation layered over explicit schemas rather than reflection-heavy runtime discovery.

## Active Architecture

### Explicit Environment

`'env` is for:

- repositories
- gateways
- domain services
- feature dependencies
- request or user context when it is part of business logic
- operational services modeled explicitly as services

The default access pattern is:

```fsharp
Flow.read (fun env -> env.Orders)
```

Plain records are still the default recommendation for local app code. They are simple, legible, easy to test, and
avoid unnecessary service boilerplate.

### Nominal Service Contracts

`IHas<'service>` and `Service<'service>.get()` are the nominal compile-time checked service story.

Use this when the static contract is worth the ceremony:

```fsharp
type IHasOrders = inherit IHas<IOrderRepository>

let saveOrder order : Flow<#IHasOrders, OrderError, unit> =
    flow {
        let! orders = Service<IOrderRepository>.get()
        return! orders.Save order
    }
```

Do not make `IHasX` the default story for all dependencies. For most feature-local code, records plus `Flow.read` are
better.

### Provider Edge

`Service<'service>.resolve()` exists for pragmatic .NET host integration when `'env :> IServiceProvider`.

Use it at host edges, glue code, prototypes, and boundary adapters. Prefer mapping provider registrations into an
explicit record or nominal contract before entering core domain workflows.

Missing provider registrations are configuration defects, not domain errors.

### Closed Ambient Runtime

Ambient runtime state is reserved for executor mechanics only. It is not the dependency model for first-party service
packages.

Ambient mechanics include:

- cancellation token access
- scope ownership
- scheduling and interruption helpers
- runtime annotations and trace metadata

Operational services such as clock, log, random, GUID, and environment variables should be modeled as explicit services
and provisioned through environments and layers.

## Scope And Layers

`Scope` and `Layer` are part of the target public architecture rather than deferred internals.

- `Scope` owns deterministic teardown
- `Layer` provisions explicit environments and service bundles
- `Flow.provide` is the main way to run flows with a built environment
- `layer { }` is the primary app-environment construction style: `let!` is dependent/sequential, while sibling `and!`
  uses `Layer.merge` / `Layer.zipPar` for independent provisioning
- `Layer.merge` combines layer outputs as tuples; it does not synthesize records, interfaces, or automatic
  `IHas<'service>` environments
- `Flow.acquireReleaseWith` is the local acquire/use/release combinator
- `Flow.acquireRelease` attaches acquired resources to the current runtime scope
- `Layer.acquireRelease` attaches provisioned service resources to the layer scope

The internal registry has been removed rather than promoted.

Axial v1 intentionally does not add tagged services or automatic service-environment merging. Multiple services of the
same type should be modeled with explicit named record fields or distinct nominal contracts. If boilerplate becomes a
real problem, prefer a future source generator that emits named environment records and `IHas<'service>` implementations
over reflection, proxy types, or hidden service maps.

## Service Packages

Service packages should focus on explicit, typed, testable system effects:

- Core: clock, log, random, GUID, environment variables
- Console
- FileSystem
- Http
- Process
- future Network and telemetry packages

`Axial.Flow` owns no operational service contracts. The contracts for clock, log, random, GUID, and environment
variables live in `Axial.Flow.PlatformService`; all target-specific implementations in that package are isolated in
its internal `Platform` module. Fable-facing public operations and test implementations remain target-neutral.

Service-package operations should use explicit services. They should normally be thin wrappers over
`Service<'service>.get()` plus live implementations and layers.

## Documentation Direction

Public docs should teach this order:

1. Use plain records plus `Flow.read` for most app dependencies.
2. Use `IHas<'T>` plus `Service<'service>.get()` for reusable named service contracts.
3. Use `Service<'service>.resolve()` at .NET host edges.
4. Use `Layer` to provision environments and base runtime bundles.
5. Treat the ambient runtime as executor mechanics only.

Docs must avoid:

- presenting first-party services as magical ambient runtime slots
- presenting registry-backed runtime as the architecture
- teaching `IHasX` as the default for every dependency
- centering `IServiceProvider` as the main app model

## Implementation Snapshot

As of 2026-06-03, core code, service packages, tests, examples, and generated reference docs use the explicit
service/layer model. Integration tests cover `Microsoft.Extensions.DependencyInjection` provider-backed base runtime
construction, typed missing-registration failures, direct `Service<'T>.resolve()` defects, and composition of the
current Console, FileSystem, Http, and Process service layers. Remaining work should improve public guide coverage and
future service packages, not ambient-core or `Flow.service` / `Flow.inject` direction.

## Open Product Questions

- Should future telemetry services live under `Axial.Services.Telemetry` or stay as runtime instrumentation adapters?
- Should process support add scoped long-running process helpers beyond one-shot `Process.execute`?
