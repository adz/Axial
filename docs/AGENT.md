---
title: For AI Agents
description: This page shows Axial usage patterns for AI assistants.
---

# For AI Agents

This page lists Axial patterns that coding agents should follow.

{{% alert title="For AI Agents" color="info" %}}
If you are an AI assistant, prioritize the patterns in the **Dependency Guidance** and **Rosetta Stone** sections below.
{{% /alert %}}

## Default Patterns

{{% alert title="Hard effect boundary" color="danger" %}}
`Axial.Flow.Process` and `Axial.Flow.HttpClient` may perform only the effect represented by their core mockable service
(`IProcess` or `IHttp`). Every additional effect must be an explicit, mockable dependency visible in the signature.
Never call `DateTimeOffset.UtcNow` or `DateTime.Now` in these adapters; inject
`Axial.Flow.PlatformService.IClock` and call `clock.UtcNow()`. Apply the same rule to random, GUID, environment,
filesystem, console, and other operational effects.
{{% /alert %}}

Use these patterns unless local code shows a different convention.

**Axial consists of three packages that can be used independently but work together**: Error Handling (plain `Result`
with a user-owned error DU for simple code without a domain model), Schema (declare a `Schema` and parse with
`Schema.parse` when modelling a domain), and Flow (the optional effects side). `Check`, `Validation`, and `Refined` are
machinery inside those areas.

When a schema lives in its own definition module, open `Axial.Schema.Syntax` and use the constructor-last pipeline:
`Schema.define<Signup> |> field "email" _.Email |> constrain emailFormat |> field "age" _.Age |> constrain (atLeast 13) |> construct create`.
Field count is unbounded. Adding `open type Axial.Schema.Syntax` also permits bare getters (`field _.Email`), which
derive the camelCased wire name from the property; explicit names are never transformed.
`field` infers built-in schemas and an owned type's intrinsic `static member Schema`, recursively
through `option`, `list`, and `Map<string, _>`. Use `fieldWith` for an explicit local schema or a type that cannot
declare that member.

For schema boundaries, use `SchemaError` as the one interpreter error shape. Lower subsystem failures with
`SchemaError.ofParseError`, `SchemaError.ofRefinementError`, or `SchemaError.ofCheckFailure`; render with
`SchemaError.render` or `RetainedParseResult.renderErrors`; map to application errors with `RetainedParseResult.mapErrors`.

For optional serialized primitives, use `Parse.optional parser` or a discoverable primitive helper such as
`Parse.intOption`. Use `Parse.optionalOr fallback parser` or `Parse.intOrDefault fallback` when absence has a
default. These functions default only on `None`; malformed present input remains a `ParseError`.

Treat raw, wire, and draft records as untrusted shapes. Pass private refined values or private aggregates into business
code when later callers must rely on an invariant. Expose named domain transitions instead of generating record-copy
updates against private aggregates. See [Recommended Patterns]({{< relref "/schema/patterns/" >}}).

`[<DeriveSchema>]` belongs on public, namespace-level wire records. The `Axial.Schema.Contracts.Build` package generates
their schema code before compile by discovering the attribute in ordinary F# compile files. Map the generated wire
value into a hand-written domain type through the domain constructor; do not use generated wire records as
invariant-bearing domain types.

For larger applications, keep Contracts, Domain, Application, Infrastructure, and Host dependencies one-way. Resolve
`IServiceProvider` in Host, then give Application a typed Flow environment. Domain should not reference wire or
infrastructure projects.

For built-in scalar refined values in schema fields, use `Axial.Schema.RefinedSchemas`:
`RefinedSchemas.nonBlankString`, `RefinedSchemas.boundedString min max`, `RefinedSchemas.slug`,
`RefinedSchemas.trimmedString`, `RefinedSchemas.positiveInt`, `RefinedSchemas.nonNegativeInt`,
`RefinedSchemas.nonZeroInt`, `RefinedSchemas.negativeInt`, and
`RefinedSchemas.nonPositiveInt`.
For refined collections, pass an item value schema:
`RefinedSchemas.nonEmptyList RefinedSchemas.slug`, `RefinedSchemas.distinctList Schema.text`, or
`RefinedSchemas.boundedList min max itemSchema`. Use `Schema.list<'item>()` when the item schema is type-directed and
`Schema.listWith itemSchema` for an explicitly configured primitive/refined
collections, including nested records.
Use `Schema.union discriminatorField payloadField [ UnionCase.create tag construct tryPayload payloadSchema ]` for tagged
F# discriminated unions. The structured data convention is an object with the discriminator field and payload field; wrong tags
diagnose at the discriminator path and payload failures diagnose under the payload path.
Use `RefinedSchemas.dateTimeOffsetRange` as a record-shaped model schema, not a field value schema. `dateOnlyRange` is
also available when targeting frameworks that support `DateOnly`.

For JSON, pick the path by trust: untrusted bodies go `Data.ofJsonDocument` (or `ofData`) then
`Schema.parse` for diagnostics; trusted payloads use `Axial.Schema.Codec` — compile once with `Json.compile schema`, then
`Json.serialize` / `Json.deserialize`. Serve the contract with `JsonSchema.generate schema`. Do not hand-write
`System.Text.Json` converters for schema-described models.

For external processes, open `Axial.Flow.Process.DSL` locally. Use `cmd $"tool {argument}"` so interpolation holes stay
atomic, connect endpoints with `=>`, and end with `capture`, `console`, `stream`, or `Output.*`:

```fsharp
Input.file "source.json"
=> cmd $"jq {filter}"
=> Output.file "result.json"
```

Use `pipe [ $"producer"; $"consumer" ]` for vertical linear pipelines, `merge` for line-framed fan-in, `pipeBothTo`
for Bash `|&`, and `mergeStderr` for the intent of `2>&1`. Prefer typed topology across platforms. Use explicit `bash`,
`sh`, or `pwsh` only when shell language is genuinely clearer; never concatenate untrusted values into `*Text` shell
programs.

For HTTP calls, open `Axial.Flow.HttpClient.DSL` locally. Build requests with `GET $"...{value}"` (interpolation holes are
URL-encoded as one value; wrap credentials in `secret` for redaction), configure with `query`, `bearer`, `timeout`,
`jsonBody`, and `expect`, then finish with `fetch`, `fetchText`, or `fetchJson decode`:

```fsharp
GET $"https://api.example.com/users/{userId}"
|> bearer token
|> fetchJson decodeUser
```

The result is `Flow<#IHas<IHttp>, HttpError, _>`. Treat expected non-2xx statuses as data with
`expect [ 200; 404 ]` rather than catching errors, and retry only transient failures with `Http.retryTransient`
(or `withRetries`); never wrap non-idempotent POSTs in retries without an idempotency key header.

### 1. Handling Failures
Use `Check` for executable value constraints, `Predicate` for local boolean tests, and `Result` for fail-fast values. `Check.*` helpers return `Result<'value, CheckFailure list>` — a passing check hands back the same value unchanged, so it pipes directly into the next step with no separate value-preserving wrapper needed. `Result.requireTrue`/`okIf`/`failIf`/`orError` and extraction helpers cover the generic, non-Check cases.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `bool` | `Result.requireTrue e condition` |
| `string` value | `name |> Check.present |> Result.orError e` |
| `option<'T>` | `opt |> Result.someOr e` |
| `voption<'T>` | `vopt |> Result.valueSomeOr e` |
| check + value | `value |> check |> Result.mapError mapper` |

### 2. Binding Error-Adapted Sources
Use `Bind.error` inside `flow {}` when the source fails with option/value-option absence or a `unit` error, and you need to assign the flow's domain error at the bind site.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Option<'T>` | `let! x = opt |> Bind.error e` |
| `voption<'T>` | `let! x = vopt |> Bind.error e` |
| `Async<Option<'T>>` | `let! x = aOpt |> Bind.error e` |
| `Async<voption<'T>>` | `let! x = aVOpt |> Bind.error e` |
| `bool` predicate | `do! Result.requireTrue () cond |> Bind.error e` |
| `Result<'T, unit>` | `let! x = check |> Bind.error e` |
| `Flow<'Env, unit, 'T>` | `let! x = flow |> Bind.error e` |
| `Task<Option<'T>>` | `let! x = tOpt |> Bind.error e` |
| `Task<voption<'T>>` | `let! x = tVOpt |> Bind.error e` |

### 3. Mapping Errors
Use `Bind.mapError` inside `flow {}` when the source already carries a meaningful error value that must be wrapped or translated before binding.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Result<'T, 'E1>` | `let! x = result |> Bind.mapError mapper` |
| `Flow<'Env, 'E1, 'T>` | `let! x = flow |> Bind.mapError mapper` |
| `Async<Result<'T, 'E1>>` | `let! x = aResult |> Bind.mapError mapper` |
| `Task<Result<'T, 'E1>>` | `let! x = tResult |> Bind.mapError mapper` |

### 4. Same-Family Fallbacks
Use `orElse` and `orElseWith` for alternate computations in the same family: `Result.orElse`/`Result.orElseWith`, `Validation.orElse`/`Validation.orElseWith`, and `Flow.orElse`/`Flow.orElseWith` all share this shape — `orElseWith` takes a function from the source error to a fallback of the same type; `orElse` is the eager version that ignores the error and always uses the same fallback.

For ASP.NET Core or GenHTTP server endpoints, keep routing native and make the handler an ordinary Flow. Bind trusted
input with `Request.json`/`form`/`query`, embed the HTTP-independent application workflow with `EndpointFlow.run`, and
return a `Response` value. Configure `flowEndpoint` once with the per-request application-environment factory and
typed-error renderer, then pass it the endpoint Flow at each route. Do not write `Func`, `Task`, `RetainedParseResult` matching,
or `Exit` lowering in the normal endpoint path; use the lower-level `SchemaRequest` API only when the endpoint needs
the complete parse result, such as form redisplay.

### 5. Flow Signatures

Start with the smallest useful Flow signature. Expand to the full `Flow<'env, 'error, 'value>` form only when a workflow needs both environment and typed failure channels:

| Alias | Use when |
| :--- | :--- |
| `Flow<'value>` | No environment and no typed failure. |
| `Flow<'error, 'value>` | No environment, with typed failure. |
| `EnvFlow<'env, 'value>` | Environment, with no typed failure. |
| `ExnFlow<'value>` | No environment, with recoverable exceptions as typed failures. |
| `ExnEnvFlow<'env, 'value>` | Environment, with recoverable exceptions as typed failures. |

Use `Flow.fromAsync`, `Flow.fromTask`, and `Flow.fromValueTask` when thrown exceptions are defects. Use `Flow.attemptAsync`, `Flow.attemptTask`, and `Flow.attemptValueTask` when expected exceptions should become `Cause.Fail exn`.

Use `App.run env rootFlow` for a finite root application. Use `App.start env rootFlow` when a signal handler, host, or
UI owner needs an `AppHandle`; await `Stop()` so scoped cleanup finishes. Platform adapters live in
`Axial.Flow.Hosting` (.NET and Generic Host), `Axial.Flow.Hosting.Node`, and `Axial.Flow.Hosting.Browser`.

### 6. Dependency Guidance

Keep application dependencies explicit in `'env`.

| Need | Idiomatic Pattern |
| :--- | :--- |
| **Direct field access** | `let! port = Flow.read _.Port` |
| **Dependency function** | `let! loadUser = Flow.read _.LoadUser` |
| **Named service** | `let! clock = Service<IClock>.get()` |
| **Whole environment** | `let! env = Flow.env` |
| **Provisioned environment** | `flow |> Flow.provide appLayer` |
| **Host boundary** | Build the environment once, or use `Service<'T>.resolve()` only in edge glue |

Prefer plain records for most application workflows. Keep `IServiceProvider` interop at the host
boundary instead of making container lookup the default model inside business logic. Use layers to
validate provider-backed services and build reusable explicit environments.

### 7. Rosetta Stone
Translate common patterns from other libraries into idiomatic Axial.

| If you use... | Do this in Axial |
| :--- | :--- |
| `requireSome` | `let! x = opt |> Bind.error e` in `flow {}` or `opt |> Result.someOr e` in pure code |
| `Reader.ask` | `let! env = Flow.env` |
| `Reader.asks` | `let! value = Flow.read projector` |
| `ZIO.service` | `let! service = Service<IService>.get()` |
| `.NET IServiceProvider.GetRequiredService` | `let! service = Service<IService>.resolve()` at the edge |
| `match x with Some...` | `let! v = x |> Bind.error e` in `flow {}` |
| `Result.mapError` | `let! x = result |> Bind.mapError mapper` in `flow {}` |
| retry policy | `flow |> Schedule.retry schedule` |
| repeat policy | `flow |> Schedule.repeat schedule` |
| ActiveModel / FluentValidation validators | `Schema<'model>` + `Schema.parse` — constraints declared once, invalid models never constructed |
| DTO + manual mapping into domain types | schema fields over refined value schemas (`Schema.convert`) |
| form redisplay with per-field errors | `parsed.Input` + `Data.tryRedisplayPath`, `parsed.ErrorsFor "contacts[1].value"` |
| workflow-specific business rules | an ordinary result-returning function or an environment-aware `Policy` |
| editable schema model | update its public draft record with `with`, followed by `Schema.check` when trust is required |
| `with` update on a private-representation aggregate | lower to its public draft record, edit with `with`, re-admit through the aggregate's `create` |
| versioned wire input | `Contract.parse` with an explicit `VersionSource` and typed migrations |
| hand-written schema for a plain wire DTO | `[<DeriveSchema>]` on the record; `schemagen` (or the `Axial.Schema.Contracts.Build` package) derives `schema`/`parse`/`validate` |
| cross-field invariant used throughout the app | private aggregate + public draft + fallible constructor; use an `.fsi` file for an opaque interface |
| update to an invariant-bearing aggregate | named transition returning `Result` when the update can be refused |
| wire DTO entering business code | explicit wire-to-domain mapping through the domain constructor |
| guard clauses at workflow entry | `Policy` + `Flow.verify` |


## Hierarchy of Effects

Later types can bind earlier types directly within their computation expressions.

1. **Check**: Executable value constraints (`'T -> Result<'T, CheckFailure list>`; a passing check keeps the value). Use `Predicate` for raw boolean tests.
2. **Result**: Fail-fast typed errors (`Result<'T, 'E>`).
3. **Refined**: Parsing and structural refined values.
4. **Validation**: Accumulating diagnostics.
5. **Schema**: Portable model metadata (`Schema<'model>`) interpreted by `Schema.parse`, `Schema.check`, `Inspect`, contracts, codecs, and test generators.
6. **Flow**: Environment-aware workflows (`Flow<'Env, 'E, 'T>`) for synchronous, async, and task-based composition.

## Machine-Readable Reference

For a compact reference, point your agent to:
`llms.txt` in the repo root, or the locally generated preview at `/llms.txt` when serving the docs site.
