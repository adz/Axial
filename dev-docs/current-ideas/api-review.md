# Pre-1.0 API review candidates

This sketch records public APIs that deserve a deliberate decision before Axial 1.0. It does not assert that every
candidate must change. For each item, test the current API in realistic application code, choose the intended shape,
update tests and user documentation, and remove the item once the decision has moved into current architecture or code.

## 1. Remove hidden randomness from `Schedule.jittered`

`Schedule.jittered` constructs `System.Random` internally and samples it while evaluating a schedule:

```fsharp
let jittered schedule =
    let random = Random()
    jitteredWith random.NextDouble schedule
```

That makes workflow timing depend on an ambient effect which an application cannot replace or control in a test. It is
in tension with Axial's rule that operational effects are explicit.

Review these options:

1. Remove `Schedule.jittered` and retain only `Schedule.jitteredWith`.
2. Put a service-based convenience function in `Axial.PlatformService` that obtains an explicit randomness service.
3. Accept a randomness source as part of schedule construction under a more descriptive API.

Do not retain a convenience API that silently creates randomness in core.

## 2. Clarify application and runtime `ActivitySource` ownership

Application workflows should normally emit from an application-owned instrumentation scope:

```fsharp
workflow
|> Activity.traceOn applicationActivitySource "orders.place"
```

Axial's automatic fiber spans and metrics correctly retain the `Axial` instrumentation scope because they describe
runtime behavior. The public `Activity.source`, `Activity.trace`, and `Activity.traceWith` defaults can nevertheless
encourage applications to place user operations under Axial's scope.

Review whether 1.0 should:

- rename `Activity.source` to `Activity.runtimeSource`;
- hide the runtime source if applications do not need direct access;
- retain or remove the default-source `trace` and `traceWith` shortcuts;
- expose an adapter value that captures an application source once, rather than requiring it at every tracing call.

One possible configured shape is:

```fsharp
let tracing = ActivityTracing.create applicationActivitySource

workflow
|> tracing.trace "orders.place"
```

Any chosen API must keep service name, instrumentation scope, and span name distinct.

## 3. Decide the `Ref.modify` tuple order and complete the atomic family

The current function expects `newState * result`:

```fsharp
Ref.modify (fun current ->
    let next = current + 1
    next, current)
```

Many functional state APIs, including ZIO's `Ref.modify`, use `result * newState`. Either order can work, but changing it
after 1.0 would be especially error-prone. Test both forms in realistic pipelines and choose one deliberately.

Also decide whether the 1.0 surface should include the predictable atomic family:

```fsharp
Ref.getAndSet
Ref.getAndUpdate
Ref.updateAndGet
```

Avoid adding aliases unless each operation has distinct return semantics.

## 4. Decide whether the public type should remain `TelemetryContext`

Users construct and scope values through the `Context` module, while public signatures expose the concrete type name
`TelemetryContext`:

```fsharp
Context.empty
Context.withAttribute attribute flow
```

This may be the right balance: `Context` alone is broad, while `TelemetryContext` is unambiguous in type annotations.
Review generated reference pages, error messages, and explicit annotations to ensure the module/type distinction is
clear. Rename only if application code demonstrates recurring confusion.

## 5. Stabilize the schedule contract

Before freezing schedules, specify and test:

- whether `Schedule.recurs 3` counts runs, retries, or recurrences;
- whether schedules are safely reusable and how state resets;
- deterministic clock and delay behavior;
- composition requirements for 1.0;
- accepted ranges and failure behavior for injected jitter samples;
- elapsed-time outputs;
- overflow and invalid-delay behavior;
- retry and repeat behavior on interruption and defects.

Keep the 1.0 schedule surface small if these semantics do not justify broader composition yet.

## 6. Remove ambient time from fiber-dump rendering

`FiberRegistry.Dump()` reads `DateTimeOffset.UtcNow`. The registry already exposes snapshots, and the rendering API has
an explicit `FiberDump.renderTreeAt now` form. Review whether the convenience method should instead require a timestamp
or whether timestamped rendering belongs in a package with an explicit clock service.

A deterministic boundary could be:

```fsharp
let snapshots = registry.Snapshot()
let rendered = FiberDump.renderTreeAt now snapshots
```

Diagnostic convenience does not automatically justify an ambient clock in core.

## Reviewed and not currently considered awkward

The abbreviated Flow type forms preserve parameter meaning consistently:

```fsharp
type Flow<'value> = Flow<unit, Never, 'value>
type Flow<'error, 'value> = Flow<unit, 'error, 'value>
type Flow<'env, 'error, 'value> = ...
```

The forms progressively add channels from right to left: value; error and value; environment, error, and value.
`'error` does not change meaning between the two- and three-parameter forms. Do not treat these aliases as a 1.0 issue
without separate evidence from application use.

## Suggested review order

1. Hidden randomness in `Schedule.jittered`.
2. Application versus Axial `ActivitySource` ownership.
3. `Ref.modify` tuple order and atomic helpers.
4. `TelemetryContext` type naming.
5. Exact schedule semantics.
6. Ambient time in fiber-dump rendering.
