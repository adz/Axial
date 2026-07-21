# Axial Plan

This file tracks current product and architecture direction.
High-level durable decisions live in `dev-docs/decisions/`.
Speculative sketches live in `dev-docs/current-ideas/`, but this file is the live direction.

## Release Strategy

Per `prd.md`: the boundary stack — the `Axial.ErrorHandling` package (hosting the `Axial.ErrorHandling`,
`Axial.Validation`, and `Axial.Refined` namespaces), `Axial.Schema`, and `Axial.Schema.Json` — is the 1.0 gate, driven by
a real adoption target (a ~100-variant versioned config system). The Flow group's remaining pre-1.0 scope in
`LATER_TODO.md` is demand-driven — pulled forward when a concrete application needs it. The contract-declaration
thread originally sequenced versioning/migration machinery before the grammar; in practice the grammar and generator
shipped first (2026-07-12, single-version wire-tier scope), and the versioning/migration engine shipped on 2026-07-13.
Record-first `[<DeriveSchema>]` generation is now the primary generated path; `.contract` remains a secondary wire-tier
form with no planned LSP investment. The schema surface has been through heavy recent churn (direct `Result` returns
and contracts) and should be treated as settling rather than settled.

## Current Direction

Axial began as a Reader-Async-Result workflow monad in the ZIO tradition; the result side has since expanded into a
full parse-don't-validate toolkit. The library is therefore two main groups, and public positioning should always
present them as such:

- **Parse-don't-validate results**: `Schema<'model>` is the front door for domain models — input parsing, intrinsic
  validation, redisplay, and metadata interpreters all fall out of one declaration. Plain `Result`
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
- data boundaries are described with portable schemas, reusable value checks, and path-aware input/validation interpreters;
  operation-specific admission uses ordinary functions or environment-aware policies

First-party service packages and standard operational services should be expressed as explicit services, not runtime
slots.

Axial's data-boundary direction splits concerns like this:

- `Check<'value>` describes reusable, path-free, raw-input-free value constraints
- `Schema<'value>` describes typed shape, construction, inspection, and portable constraint metadata; `Schema.parse`
  admits structured data and `Schema.check` rechecks an already assembled typed value through its field schemas and record
  constructor. Successful operations return the ordinary value rather than a universal trust wrapper.
- schema interpreters parse structured data, check existing values, produce diagnostics, and drive non-validation metadata
  consumers
- policies adapt checks, parsers, validations, and application admission functions into `Flow`

Core schema declarations and their interpreters share the single `Axial.Schema` namespace and package (module names,
not namespaces, separate declaration from interpretation); the package stays independent of flow execution.

Constructor-level intrinsic errors are a second stage after field parsing and field constraints, not an error source that
runs alongside invalid fields. If any field or nested item has intrinsic diagnostics, interpreters must not apply the
model constructor; constructor errors are reported only when every constructor argument is already trusted. By default
constructor errors attach to the current object path, and input parsing may expose an option to attach them to a relative
field path when that gives better boundary feedback.

Schema work should prove the portable metadata model before growing broad interpreters. The metadata slice — field
ordering, primitive value schemas, schema constraints as inspectable metadata, lowering those constraints to `Check`,
and constructor/getter alignment — is proven. Constructor-last object shapes are the sole public authoring surface:

```fsharp
Schema.define<Customer>
|> field "id" _.Id
|> field "name" _.Name
|> construct ctor
```

`Schema.define<'model>` anchors the model type before the first field. `field` infers common primitive, option, and list
schemas; `fieldWith` accepts an explicit value schema. The shape's phantom type records field types and lets
`construct` or `constructResult` match the closing constructor by arity and position. Constraints remain beside the
current field through the typed `constrain` operation.
Build-time generation exists as wire-tier tooling: `[<DeriveSchema>]`-marked records are the
primary declaration (FCS syntax-only frontend in `src/Axial.Schema.Contracts`, run by `scripts/schemagen` or the
`Axial.Schema.Contracts.Build` MSBuild package), with `.contract` files as the parked secondary form. Generated contracts
remain wire-tier records; domain models stay hand-written F# rather than becoming a second generated authoring surface.

The public schema-authoring vocabulary keeps inferred `field`, explicit `fieldWith`, and typed `constrain` operations.
`Schema.text`, `Schema.int`, `Schema.decimal`, `Schema.bool`,
`Schema.date`, `Schema.dateTime`, and `Schema.guid` are the primitive `Schema<'value>` values, and composites
(`Schema.list<'item>()`, `Schema.option`, `Schema.map<'item>()`, `Schema.union`, `Schema.inlineUnion`, `Schema.enum`, `Schema.defer`)
and refined/domain schemas fill `fieldWith`'s schema slot. Do not introduce competing primitive aliases such as `string`, `integer`,
`boolean`, `uuid`, `dateOnly`, or `Field.text`; the `Value` module is internal implementation, not public vocabulary.

Collection members are type-directed. `field` recursively resolves list item schemas, while standalone lists and
string-keyed maps use `Schema.list<'item>()` and `Schema.map<'item>()`. `listWith` and `mapWith` are the explicit escape
hatches for recursive or locally configured member schemas. `Syntax.constrainItems` and `Syntax.constrainValues` apply
typed constraints inside a collection; ordinary `Schema.constrain` applies to the collection itself. Non-string map
keys have no inferred wire representation.

Schema must also preserve a high-performance codec lowering path. The inspectable schema model may contain rich metadata,
but JSON codecs should not interpret that metadata tree directly on the hot path. A codec interpreter must be able to
compile schemas into direct record plans: ordered field descriptors, cached wire-name bytes, indexed field slots,
typed field decoders, and constructor application that does not require per-value reflection or `obj array` dispatch.
CodecMapper is the performance reference for this shape. This path now ships as `Axial.Schema.Json` (`Json.compile` over the
retained compiled record plan, benchmarked against `System.Text.Json` in `benchmarks/Axial.Benchmarks/CodecSuites.fs`);
remaining codec work is optimization and format breadth, not proving the shape.

The built `Schema<'model>` value itself must retain typed constructor and field information sufficient for that codec
compilation: type erasure at authoring time must not force interpreters onto boxed `obj array` dispatch or require
callers to re-supply the constructor and typed fields alongside the schema. Codec compilers walk the retained typed
shape to emit constructor-specialized record plans (CodecMapper's `MappingDefinition` / `Specialize` dual-view pattern).

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

### Root Applications And Hosts

`App` owns the portable lifetime of one root Flow application. `App.run` is the finite entry point; `App.start` returns
an owned handle whose `Stop()` is idempotent and whose `Completion` becomes available after root scope cleanup. App
definitions stay ordinary provided Flow values rather than inheriting a host-specific base type.

Platform hosting packages translate native events into `App.Stop()` and translate the final `Exit` at the outer edge:

- `Axial.Flow.Hosting`: standalone .NET console and Microsoft Generic Host, plus MEL adaptation
- `Axial.Flow.Hosting.Node`: Node signals, process exit, arguments, and `process.env`
- `Axial.Flow.Hosting.Browser`: UI ownership and `AbortSignal`

The browser adapter never equates tab visibility or unload with dependable application shutdown. Node and browser
packages are JavaScript-only Fable bindings and fail immediately outside their named runtime.

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

- Core (`Axial.Flow.PlatformService`): clock, log, random, GUID, environment variables
- Console (`Axial.Flow.Console`)
- FileSystem (`Axial.Flow.FileSystem`)
- Http (`Axial.Flow.HttpClient`)
- Process (`Axial.Flow.Process`)
- Telemetry (`Axial.Flow.Telemetry`) and hosting adapters (`Axial.Flow.Hosting`)
- future Network package

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

As of 2026-07-12, core code, service packages, tests, examples, and generated reference docs use the explicit
service/layer model. Integration tests cover `Microsoft.Extensions.DependencyInjection` provider-backed base runtime
construction, typed missing-registration failures, direct `Service<'T>.resolve()` defects, and composition of the
current Console, FileSystem, Http, and Process service layers. Effect dependencies are explicit through the stack:
`Process.live`/`Process.layer` take `IClock`, `IFileSystem`, and `IConsole` rather than touching files or the host
console ambiently, and `Script.run` takes an explicit `IConsole` and returns the exit code. Remaining work should
improve public guide coverage and future service packages, not ambient-core or `Flow.service` / `Flow.inject`
direction.

`Axial.Flow.Process` uses one immutable `ProcessSpec` construction model. `IProcess.Run` returns a lazy
`Flow<unit, ProcessError, ProcessResult>` and `IProcess.Stream` returns a lazy event stream. `Process.run` and
`Process.stream` compose those programs into the caller's environment. Flow owns timeout racing, cancellation, and
scope cleanup; the native interpreter owns process-tree termination and partial-start cleanup.
