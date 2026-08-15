# Pre-1.0 API review candidates

This sketch records public APIs that deserve a deliberate decision before Axial 1.0. It does not assert that every
candidate must change. For each item, test the current API in realistic application code, choose the intended shape,
update tests and user documentation, and remove the item once the decision has moved into current architecture or code.

## 1. Clarify application and runtime `ActivitySource` ownership

Application workflows should normally emit from an application-owned instrumentation scope:

```fsharp
workflow
|> Activity.traceOn applicationActivitySource "orders.place"
```

Axial's automatic fiber spans and metrics correctly retain the `Axial` instrumentation scope because they describe
runtime behavior. The public `Activity.source`, `Activity.trace`, and `Activity.traceWith` defaults can nevertheless
encourage applications to place user operations under Axial's scope.

Decision: introduce an `ActivityTracer` adapter that captures an application source once, and drop the
default-source `trace`/`traceWith` shortcuts that currently make it easy to tag application spans under Axial's own
`ActivitySource`. Also rename `Activity.source` to `Activity.runtimeSource` (or hide it) so it reads unambiguously as
Axial-internal, not a general-purpose default.

```fsharp
let appTracer = ActivityTracer.create appActivitySource

workflow
|> appTracer.trace "orders.place"
```

Decided: ambient, not explicit-via-`'env`. Route the tracer through `RuntimeContext` the same way Axial already
threads `TelemetryContext`, `AnnotationSink`, and `Observer` — a fiber-scoped cell, inherited by forked children, not
a process-wide global. Install it once near the composition root, e.g. `Flow.Runtime.withTracer appTracer workflow`.
Routing it through `'env` instead would put a tracer-capability constraint on every workflow's environment type
across the whole public API, disproportionate to the problem and inconsistent with how every other cross-cutting
concern in Axial already works.

"By construction" here means no silent wrong answer, not compile-time proof: if `Activity.trace` runs with no
tracer installed, it must throw immediately rather than default to Axial's own `ActivitySource`. That converts the
original defect (spans silently mistagged under the wrong scope) into a loud, obvious startup-composition failure
instead of a compile-time impossibility — which is sufficient, and consistent with the rest of the ambient
`RuntimeContext` design.

`ActivityTracer.create` remains the only way to obtain a value with a `.trace` member for the explicit call-site
form (`appTracer.trace name flow`); the ambient path is the convenience layer on top of it, not a replacement.

Any chosen API must keep service name, instrumentation scope, and span name distinct.

## 2. Stabilize the schedule contract

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

1. Application versus Axial `ActivitySource` ownership.
2. Exact schedule semantics.
