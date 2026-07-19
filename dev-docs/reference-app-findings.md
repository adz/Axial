# Reference App Findings: What Axial Earned, What It Did Not

> Status: this report records the application exercise before the Schema reorientation. The implementation now uses
> one `Schema<'value>` catalog, `Schema.parse`, `Schema.check`, fallible `Schema.refine`, bare successful contract
> results, and `RefinedSchemas`. Old API names remain below where they identify the evidence that prompted a change.

## 2026-07-12 construction re-review

The current reference app supports a narrower construction recommendation than the earlier `Model<'value>` / universal
draft direction:

- A reusable field schema is an ordinary `Schema<'field>` value. It may be named (`Contracts.workspaceName`) or
  attached through `fieldWith`; these are the same mechanism.
- Portable constraints stay on that field schema because parsing, diagnostics, inspection, JSON Schema, and test-data
  generation need declarative metadata. They are not a second record-field API and they do not replace a domain
  constructor.
- Important field-local guarantees belong to private refined types with authoritative smart constructors. The app's
  `WorkspaceName`, `PersonName`, and `WorkItemTitle` demonstrate this: ordinary application code calls `create`, while
  `Schema.refine` calls that same constructor at boundaries.
- A public aggregate record is appropriate when its fields already carry their durable guarantees and its relational
  changes go through domain transitions. `Workspace` does not need a parallel draft merely to support ordinary record
  construction or `with` updates.
- Introduce a separate public draft only when the aggregate itself has a private representation and callers need named
  assembly or multi-field editing before re-admission. Drafts are a solution to private cross-field aggregates, not the
  default Schema construction model.
- Public wire records such as `WorkspaceV2` are already draft-like boundary representations. Parse external values with
  `Schema.parse`; use `Schema.check` only when admitting an already assembled typed wire/draft value whose construction
  history is not trusted.

The reference app should therefore teach regular `field`/`fieldWith` composition with the constructor last. It
should not add positional field constraints, primitive-specific field builders, a second value-schema catalog, or a
universal trust wrapper.

Direct smart-constructor failures from `createWorkspace`, `addMember`, `addWorkItem`, and `rename` use the structured
`AppError.InvalidValue of RefinementError` case. They remain distinguishable from schema diagnostics and domain
transition failures without being mislabeled as persistence errors or flattened to strings before rendering.

This report evaluates Axial through the implementation of `examples/Axial.ReferenceApp`: a workspace tracker with
related domain types, four schemas, refined values, smart constructors, contextual rules, Flow application services,
CLI commands, HTML forms, a JSON API, versioned JSON files, migration, and focused tests.

This is not an API inventory or a roadmap argument. It records what was useful while building application code, what
created friction, what remained speculative, and what should change before the current shapes become difficult to
remove.

## Executive assessment

Axial has two pieces worth being loud about:

1. A schema can be an explicit, typed, reflection-free boundary declaration that drives parsing, diagnostics,
   inspection, JSON Schema, codecs, generated test data, and versioned contracts.
2. Flow is a readable typed workflow model when an application genuinely needs explicit dependencies, typed failures,
   cancellation, resources, and execution policy in one value.

The strongest product is not “many validation abstractions.” It is one trusted-boundary story:

```text
untrusted input -> RawInput -> Schema -> path diagnostics -> domain value
```

with optional interpreters around the same declaration.

The weakest area is the trust-wrapper and refined-value ergonomics. `Model<'value>` currently makes the trust story
less coherent, not more coherent. The built-in refined catalog proves useful invariants, but ordinary programming pays
repeated unwrapping costs and `Value.refined` does not compose naturally with the `Result`-returning smart constructors
the rest of Axial encourages.

The reference app also reinforced a scope warning: Axial should own semantic boundary machinery and Flow composition,
but should not grow replacements for mature platform infrastructure merely because ZIO has an equivalent module.

## Excellent: preserve and emphasize

### One explicit schema, several real interpreters

The most valuable idea is that `Schema<'model>` is inspectable data while retaining a typed constructor/field chain.
That combination is unusual and genuinely useful. It gives Axial a credible story across:

- parsing configuration, forms, and JSON-shaped input;
- accumulated, path-aware diagnostics;
- JSON Schema and form metadata;
- compiled trusted-lane codecs;
- schema-derived FsCheck generators;
- versioned wire contracts;
- AOT, trimming, and Fable constraints without reflection as the foundation.

This is substantially better than maintaining unrelated DTO validation, OpenAPI annotations, form metadata, test
generators, and serializers. The value is strongest when every interpreter is demonstrably driven by the same schema,
not merely when several APIs happen to accept it.

The constructor-last shape is ergonomic in practice:

```fsharp
Schema.define<WorkspaceV2>
|> field "version" _.version
|> field "id" _.id
|> fieldWith workspaceName "name" _.name
|> construct ctor
```

It is explicit, local, searchable, compiler-checked, and does not require reflection or a bespoke computation
expression. This is one of Axial's clearest differentiators and should be shown prominently.

### RawInput as a common boundary currency

`RawInput` provided real value. JSON and HTML forms could converge before schema parsing without forcing the domain or
schema to know which transport produced the data. Retaining raw input for redisplay is particularly valuable for HTML
forms and configuration editors.

The model works because `RawInput` stays small and JSON-like. It should remain a boundary representation, not become a
general dynamic-value system.

### Path-aware accumulated diagnostics

The diagnostics model worked well. Errors could be associated with `name`, nested members, or collection items and
rendered differently at CLI, form, and HTTP boundaries. Accumulation is materially better than fail-fast smart
constructor chains for user-facing forms and configuration files.

Typed `FieldRef` paths are also a strong idea. They prevent contextual-rule paths from silently drifting when a wire
field is renamed. That is exactly the sort of small safety feature users appreciate after the first refactor.

### Versioned contracts that revalidate migrations

The contract chain was useful and pleasantly explicit:

- version selection is not guessed;
- only contiguous migrations can be registered;
- old versions parse through their own schema;
- migration failures differ from parse failures;
- migrated output is revalidated against the current schema;
- storage can read old versions while writing only the head version.

Revalidation was the excellent, slightly surprising part. Migration functions are ordinary code and can accidentally
produce invalid current values; treating migration as trusted would create a hole in the boundary model. Axial closes
that hole.

### Flow for application orchestration

Flow made the application use cases readable once persistence and identifier generation were explicit environment
dependencies. Typed domain/storage failures remained separate from defects and cancellation, and workflows composed
without turning every function into `Task<Result<...>>` plumbing.

The useful Flow pitch is not monadic novelty. It is that dependency requirements, recoverable failure, async execution,
cancellation, and resource behavior remain explicit and composable. For applications already comfortable with
`Async<Result<_,_>>`, Flow earns its cost when several of those concerns occur together.

## Good, but with real costs

### Separating wire records from domain records

The reference app used public wire records for schema parsing and private-constructor domain types for business logic.
This was conceptually clean and made version migration honest: old wire shapes are not pretend domain models.

The cost is duplication and conversion code. For four small schemas it was acceptable; for a hundred variants it may
become the dominant authoring burden. Axial should not pretend this cost is absent. The contract generator may help at
the wire tier, but generated wire records do not eliminate deliberate domain conversion.

The recommended architecture should be conditional:

- use one type when the boundary shape genuinely is the domain shape and its constructor can enforce invariants;
- use separate wire/domain types when versions, transport compromises, security, or domain invariants make that
  distinction valuable;
- do not mandate DTO duplication as ceremony.

### Contextual rules

Plain functions plus `ContextRules.apply` were appropriately small. They kept production-only policy separate from
intrinsic schema validity and accumulated failures correctly.

The middle ground is discoverability. A list of functions is flexible but does not itself explain which context chose
the list, whether rules are exhaustive, or where they run. This is probably the right tradeoff; a richer rules engine
would add more concepts than value. Documentation should show rule selection beside the use case that supplies the
context.

### Compiled codecs

The codec was a good storage write path because the value had already passed the boundary. The explicit distinction
between validating parse and trusted codec is sound and performance-friendly.

It is also easy to misuse. `Json.deserialize` does not enforce all schema constraints, so its type can look more trusted
than its semantics. Naming and documentation must keep “trusted lane” impossible to miss. A codec must not become the
default public-input parser merely because it is faster.

### The built-in refined catalog

`NonBlankString`, positive numbers, slugs, ranges, and non-empty collections are useful reusable invariants. Private
constructors do preserve validity; the original concern that they do not maintain their invariant was not borne out.
Once constructed, `NonBlankString` cannot become blank without leaving the type.

The ergonomic concern was absolutely borne out. The invariant is preserved, but the value does not behave like its
underlying primitive in ordinary code. `NonBlankString` requires `.Value` or `NonBlankString.value` before using string
APIs. A domain wrapper such as `WorkspaceName` adds another conversion boundary.

There is a hard platform fact here: a nominal F# type cannot literally be `System.String`; `String` is sealed and type
abbreviations add no invariant. “Behaves like a string” must therefore mean excellent forwarding and conversion
ergonomics, not inheritance or transparent type identity.

## Flat-out change

### Make the Model trust story consistent

The current API has three conflicting shapes:

- `Model.parse` returns `ParsedInput<'model, SchemaError>` whose success is bare `'model`;
- `Model.validate` returns `Model<'model>`;
- `Contract.parse` returns `Model<'model>`.

This was genuinely surprising. The same conceptual operation—admitting a value through schema authority—sometimes
wraps and sometimes does not. Application code cannot infer whether `Model<'model>` is the canonical trusted domain
type, a boundary receipt, or an implementation detail.

The reference app's most natural shape was to consume trust evidence once at the boundary and pass a bare domain value
whose private constructors maintain its important invariants. Carrying `Model<Workspace>` through repositories and
business functions would have been noise. It would also imply that a model becomes untrusted merely because legitimate
domain logic returned an updated record without rewrapping it.

Recommended change before 1.0:

1. Decide whether schemas produce trusted values or proof wrappers.
2. Prefer bare values from parsing when the schema constructor is the admission boundary.
3. If a wrapper remains useful for validating freely-constructible drafts, name it for that specific guarantee, such
   as `Validated<'value>` or `SchemaChecked<'value>`, and do not make it the universal domain type.
4. Make parse, validate, reconstruct, and contract migration return shapes consistent with that decision.

`Model<'value>` as a broad public workflow concept should not survive merely because it already exists.

### Let refined schemas use fallible smart constructors

`Value.refined` accepts:

```fsharp
'raw -> 'value
```

but normal smart constructors accept:

```fsharp
'raw -> Result<'value, 'error>
```

The reference app had to declare raw constraints and then call an internal constructor that assumes those constraints
already ran. This manually duplicates the invariant and creates a maintenance hazard: change the smart constructor but
forget the schema constraints, and the supposedly safe total constructor can throw or admit the wrong thing.

Axial already has the necessary error-mapping concepts. It should provide a refined-schema constructor that accepts a
fallible smart constructor and maps its failure into schema diagnostics. Metadata constraints can still be supplied for
inspection, JSON Schema, forms, and generators, but executable correctness should have one authority.

The API should make the relationship explicit rather than pretending metadata and construction cannot disagree.

### Improve primitive-like refined operations

Do not add implicit conversions everywhere; they obscure validation boundaries and can behave poorly across Fable and
generic code. Do add consistent, unsurprising operations:

- `value`/`toString` everywhere;
- comparison and equality that follow the underlying value;
- `length`, `isEmpty` where meaningful;
- safe `map`/`bind` operations that recheck invariants;
- common non-mutating string queries on string refinements;
- collection operations for non-empty collections that preserve non-emptiness when mathematically guaranteed;
- documented interop conventions for .NET APIs.

The goal is that users unwrap at external API boundaries, not every other line of domain logic.

### Stop mapping validation failures to storage strings

The sample exposed a missing ergonomic bridge from `RefinementError` or schema diagnostics into an application's own
error DU. Some application functions temporarily mapped smart-constructor failures into `AppError.Storage` strings
because the small app lacked a clean shared admission-error type. That is an example smell, not a recommended pattern.

The reference architecture should grow a first-class `InvalidValue` application error case and preserve structured
failures. Axial documentation should show this translation explicitly. `BindError` may help at Flow bind sites, but the
underlying error vocabulary still belongs to the application.

## Likely speculative garbage unless demand proves otherwise

“Garbage” here means an API direction with no demonstrated user value proportional to its maintenance cost, not that
the underlying computer-science concept is invalid.

### Broad ZIO parity as a roadmap

Transactional collections, hubs, custom queues, fiber-local state, runtime flags, supervision frameworks, differ/patch
infrastructure, custom metrics primitives, channel internals, and a large sink/pipeline hierarchy are not justified by
the reference app. Most are ideas imported from a different runtime ecosystem rather than needs discovered in .NET
applications.

They should remain absent until a concrete Axial application cannot be served well by existing .NET libraries. A TODO
line is not evidence of product value.

### Axial-owned concurrency should extend proven Flow semantics

.NET already has `Channel<T>`, `SemaphoreSlim`, concurrent collections, `TaskCompletionSource`, cancellation tokens,
`IAsyncEnumerable`, TPL Dataflow, and mature reactive/streaming libraries. Axial wrappers are worthwhile when they add
specific typed-failure, scope, interruption, backpressure, or environment semantics that cannot be expressed cleanly
with a thin adapter.

`Axial.Flow.Process` is concrete evidence that this can be worthwhile: its bounded stream of structured process output
connects process lifetime, cancellation, completion, typed failure, and backpressure in one application-facing model.
That is not speculative parity; it solves a real operational workflow. Future concurrency should be derived from more
work of that kind, with shared primitives extracted only after repeated use.

Owning a general queue, STM runtime, or hub still creates a large correctness and performance liability. The current
global-lock STM is difficult to recommend for production without a concrete use case and serious concurrency evidence.
The distinction is between application-proven concurrency semantics and catalog completion for its own sake.

### Generalize streaming from Process, not from a parity checklist

Streaming has already earned a place in Process. Long-running stdout/stderr, bounded delivery, cancellation, and a final
structured completion event are realistic requirements, and a one-shot `Flow<ProcessResult>` alone is not sufficient.

The remaining question is how much of that implementation should become general `FlowStream`. Constructors, sinks,
channels, pipelines, compression, buffering, and scheduling still form an entire product. Extract the smallest reusable
model from Process and the next real streaming consumer, test it against `IAsyncEnumerable` and
`System.Threading.Channels`, and expand only where Flow-specific finalization and typed-cause behavior provide value.

### More validation layers

Axial already has `Check`, `Result`, `Refined`, `Validation`, `Diagnostics`, `Schema`, contextual rules, policies, and
`Model`. Each can be explained individually, but the total cognitive load is high. Adding advisory validation, another
rules container, dynamic values, or more wrapper types would make the product worse.

The public teaching story should aggressively collapse to two doors:

- plain `Result` for straightforward application logic;
- `Schema` for structured boundaries and domain models.

Everything else should be introduced only as machinery used by those doors.

## Better served by integration

### Persistence

Use `System.Text.Json`, database drivers, EF Core, Dapper, or application-owned repositories. Axial should provide
boundary parsing, codecs, and Flow adapters—not an ORM, document database, migration framework for storage engines, or
transaction manager.

The local JSON store was useful for proving versioned contracts, but it is not a production persistence recommendation.

### HTTP and hosting

ASP.NET Core should own routing, model transport, authentication, authorization, DI, logging configuration, OpenAPI
hosting, and response negotiation. Axial should integrate schemas and Flow at those boundaries. A focused endpoint
filter/model binder/result adapter would provide more value than building an Axial web framework.

The reference app had to hand-write repeated endpoint adaptation. This is a strong integration opportunity.

### Forms and UI

Schema inspection can provide field metadata and diagnostics, but established UI frameworks should own controls,
accessibility, localization, antiforgery, progressive enhancement, and client state. Axial should expose stable metadata
and adapters rather than a form framework.

### Observability

Use `ActivitySource`, OpenTelemetry, `ILogger`, and host-owned exporters. Axial's responsibility is propagating Flow
metadata and recording typed exits, defects, cancellation, and fiber context correctly. Custom tracing or metrics
backends would be wasted effort.

### Resilience

Basic Flow schedules are useful for typed retry/repeat composition, but production HTTP resilience is better integrated
with `Microsoft.Extensions.Http.Resilience`/Polly and host-configured `HttpClient`. Axial should not hide handler
lifetime, connection pooling, circuit breakers, or client ownership behind a parallel stack.

### Testing

FsCheck integration is valuable because schemas can derive valid boundary inputs. Keep integrating with xUnit, FsCheck,
and established test hosts. Do not build an Axial test framework.

## Missing from the current experience

### Boundary adapters

The same adaptation appeared repeatedly:

- JSON body -> `JsonDocument` -> `RawInput` -> schema parse -> HTTP result;
- form collection -> configuration paths -> `RawInput` -> schema parse -> redisplay;
- schema/context diagnostics -> application error -> transport representation;
- Flow exit -> HTTP/CLI response.

Small official ASP.NET Core and CLI adapters could remove boilerplate without owning the host. They should be explicit
and overridable, especially for error rendering.

### A canonical diagnostics renderer contract

The app wrote its own path formatting. Axial has `SchemaError.render`, but applications repeatedly need a stable DTO
such as `{ path; code; message }`, plus plain-text and problem-details renderers. Codes must remain machine-readable;
messages should be localizable and application-controlled.

### Better versioned output support

Contracts parse old versions and expose the head schema, but the “always write latest” workflow is assembled manually
with `Contract.headSchema`, conversion, and `Json.compile`. A small explicit head-codec helper or documented recipe would
reduce mistakes. Writing old versions should remain opt-in and probably uncommon.

### Update and patch boundaries

CRUD quickly raises partial-update questions. A create schema and a full persisted schema are not automatically a good
PATCH schema. The reference app avoided pretending otherwise. Axial needs a deliberate story for drafts/patches before
claiming complete CRUD ergonomics—probably explicit application-authored patch schemas, not magical optionalization of
every field.

### Schema evolution tooling

Runtime migrations worked, but maintainers still need contract-diff tooling: removed fields, changed constraints,
renames, incompatible type changes, and missing migration hops. This is a better investment than adding more runtime
validation abstractions.

### Real concurrency and file-store guidance

The sample uses temporary-file replacement, which protects against partial writes, but does not solve concurrent
multi-process updates, compare-and-swap, locking, or lost updates. The docs state that it is not a database. A stronger
production sample should use a real persistence integration and optimistic concurrency.

### End-to-end generated-contract dogfood

The reference app hand-authors schemas so the ergonomics remain visible. A companion slice should use `.contract`
generation for wire types and compare the authoring, migration, diagnostics, and domain-conversion experience. Without
that dogfood, generator value remains only partly demonstrated.

## Genuinely surprising findings

1. `Model.parse` does not return `Model<'model>`, despite the name and documentation framing, while `Model.validate` and
   `Contract.parse` do. This inconsistency was more confusing than the wrapper itself.
2. `Value.refined` is total. The natural smart-constructor shape used throughout the domain is fallible, so the schema
   bridge requires duplicated assumptions.
3. Contract migration revalidation is stronger than expected and prevented a real category of trust hole.
4. The explicit schema builder remained readable with nested and versioned records. It did not need a computation
   expression or reflection to feel usable.
5. `RawInput` was more valuable than expected because form redisplay and JSON parsing genuinely shared it.
6. Refined values do preserve their invariant; the real problem is primitive interop and wrapper stacking, not loss of
   validity.
7. The reference app needed much less of Axial's catalog than the size of the repository suggests. Schema, diagnostics,
   contracts, codecs, a few refined types, and core Flow covered nearly everything.

## What to shout from the rooftops

- Explicit, typed, reflection-free schemas that remain inspectable.
- One declaration driving path-aware parsing, metadata, JSON Schema, codecs, test generation, and version migration.
- Migration output revalidated against the current contract.
- AOT/trimming/Fable-conscious design rather than runtime-reflection convenience that fails later.
- Flow's explicit combination of environment, typed failure, cancellation, scope, and execution.
- Raw-input retention for high-quality form/configuration diagnostics and redisplay.

These claims should be backed by runnable examples and benchmarks, not broad “ZIO for F#” positioning.

## What to stop saying or implying

- Do not imply that every domain value should be carried as `Model<'value>`.
- Do not present every helper subsystem as a peer entry point.
- Do not imply that a compiled codec validates untrusted input fully.
- Do not imply that Axial needs equivalents of the entire ZIO module catalog.
- Do not call local JSON persistence or the current STM production architecture without evidence. Treat Process
  streaming as proven, while keeping broader stream claims proportional to the consumers that exercise them.
- Do not claim refined wrappers behave transparently like primitives; explain the interop tradeoff honestly.

## Recommended next decisions

1. Resolve `Model<'value>` before further schema API expansion.
2. Add a fallible refined-schema constructor driven by smart constructors.
3. Standardize refined primitive/collection operations and conversion conventions.
4. Add structured application admission-error examples; remove stringly error mapping from the reference app.
5. Build thin ASP.NET Core boundary adapters for schema parsing, diagnostics/problem details, and Flow execution.
6. Add an explicit head-version codec recipe/helper for contracts.
7. Dogfood generated wire contracts in a second reference-app slice.
8. Continue concurrency and streaming from concrete consumers such as Process; freeze parity-driven catalog expansion
   that is not exercised by an application.
9. Invest in schema/contract diff tooling before adding more validation concepts.
10. Keep the public product story to Schema, plain Result, and Flow; treat the remaining modules as supporting
    vocabulary.

## Bottom line

The reference app validated Axial's central thesis but not its full surface area. Schema plus interpreters is a strong,
differentiated product. Contracts, diagnostics, RawInput, and the typed builder materially improve real boundary code.
Flow is useful when orchestration complexity warrants it.

The wrapper story is not ready. `Model<'value>` is inconsistent, refined-schema construction duplicates smart
constructor logic, and primitive-like refined values require too much ceremony. Those are pre-1.0 design problems and
should be changed rather than documented around.

The rest of the roadmap should be judged ruthlessly by application demand. Axial becomes more compelling by making its
boundary declaration and workflow core exceptionally coherent—not by accumulating every abstraction available in its
inspiration ecosystem.
