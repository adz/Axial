# Telemetry Expansion: Activities, Annotations, and Logging

> **Status: implemented (2026-07-14).** All seven pieces shipped: `Activity.trace`/`traceWith` span the
> execution and stamp exits; `Flow.addAnnotationSink` composes sinks; `FiberTelemetry.observeWithSpans`
> gives fibers real spans; `IHasTelemetryTags`; `ILog.LogException` + `FiberLogging` in Hosting +
> `FiberObserver.compose` (and `LogEntry` removed); `Flow.tracedError`; propagation test matrix.
> Tag vocabulary documented in `docs/flow/telemetry/_index.md`. Tracked in `LATER_TODO.md` §7.

## Problem

Axial's observability surface is half-built. The pieces that exist are individually sound —
runtime annotations with a live sink, an `ActivitySource` wrapper, environment identity traits,
an `ILog` service with an MEL bridge, and the new fiber observer — but they were added at
different times and do not yet compose into one story. An application that wants "every workflow
is a span, every span carries the exit, every background failure is logged" currently has to
hand-wire most of it, and the one combinator that looks like it does this (`Activity.trace`)
is broken for async work.

### What exists today (verified against source)

| Piece | Where | State |
| --- | --- | --- |
| `Flow.annotate`, `Flow.traceId`, `Flow.Runtime.annotations`/`traceId` | `Axial.Flow/Flow.fs` | Working; map-based, nested override, inherited by forked fibers |
| `AnnotationSink` on `RuntimeContext`, `Flow.withAnnotationSink` | `Axial.Flow/Core.fs`, `Flow.fs` | Working, but installing a sink **replaces** the previous one |
| `Activity.trace` | `Axial.Flow.Telemetry/Telemetry.fs` | Defective for async (below); no exit mapping |
| `IHasRequestId`/`IHasCorrelationId`/`IHasTenantId` traits | `Axial.Flow/Runtime.fs` | Working; hardcoded trio in `Activity.trace` |
| `FiberObserver`, `FiberTelemetry.observe` | `Axial.Flow`, `Axial.Flow.Telemetry` | Working; defect spans are point-in-time, not fiber-duration spans |
| `ILog`, `Log.trace`…`Log.critical`, `Log.fromSink`, `Log.layer` | `Axial.Flow.PlatformService` | Working; message-only, no exception/structured overloads |
| MEL bridge (`ILoggerFactory` → `ILog`) | `Axial.Flow.Hosting/Hosting.fs` | Working; one logger category `"Axial.Flow"` |
| `LogLevel`, `LogEntry` | `Axial.Flow/Core.fs` | `LogEntry` is defined and used by nothing |
| `Cause.Traced`, `Cause.traced`, `Cause.prettyPrint` | `Axial.Flow/Outcome.fs`, `Core.fs` | Consumable, but **no combinator ever produces `Cause.Traced`** |

### The defects and gaps

1. **`Activity.trace` ends spans before async work completes.** The wrapper does
   `use activity = source.StartActivity(name)` inside the `Flow` lambda and returns the started
   `Execution` without awaiting it, so `Dispose` runs when the workflow is *started*, not when it
   settles. Span durations measure synchronous setup; everything async happens after the span
   closed. This is the root defect the whole workstream hangs off.
2. **Spans say nothing about outcomes.** No `ActivityStatusCode` from the exit, no typed-error
   tag, no defect exception recording, no interruption/cancellation tag. `LATER_TODO.md` §7
   names exactly this ("exit tagging, typed-error and defect recording, cancellation tagging,
   activity status, and available fiber/runtime metadata").
3. **Annotation sinks do not compose.** `Flow.withAnnotationSink` overwrites the context's sink,
   so nesting two `Activity.trace` calls means the inner span steals annotation delivery from the
   outer one, and any user-installed sink is silently dropped inside a traced region.
4. **Fiber spans are events, not spans.** `FiberTelemetry` starts and immediately stops an
   activity when a fiber settles with a defect. `FiberObserver.OnStart`/`OnEnd` already bracket
   the fiber's real lifetime with a shared `FiberMetadata` instance, but nothing uses them to
   open a span at fork (capturing the correct parent from `Activity.Current`) and close it at
   settle. Today the defect span's parent is whatever `Activity.Current` happens to be on the
   settle thread — incidental, often wrong.
5. **The identity-trait trio is closed.** `Activity.trace` special-cases `IHasRequestId`,
   `IHasCorrelationId`, and `IHasTenantId`. An environment with any other ambient identity
   (session, job id, region) has no trait to implement; its only path is manual annotations.
6. **Logging and tracing don't meet.** `ILog` takes a bare string — no exception overload, so a
   defect logged through it loses its stack trace; `LogEntry` (level/message/timestamp) is dead
   weight nothing constructs; the fiber-observer→MEL wiring shipped as a docs recipe rather than
   as code in `Axial.Flow.Hosting`, which already depends on MEL and is the right home.
7. **`Cause.Traced` is a write-only channel.** The cause tree supports trace attachment and
   pretty-printing, but no combinator produces it, so the feature is unreachable without
   constructing causes by hand.

## Design principles

Same boundaries the fiber-observer work established:

- **`Axial.Flow` stays zero-dependency.** Core owns data and hooks (annotations, observer,
  cause traces); it never references `System.Diagnostics.DiagnosticSource` or MEL.
- **`Axial.Flow.Telemetry` owns Activity mapping.** All span conventions live there.
- **`Axial.Flow.Hosting` owns MEL integration.** Anything typed against `ILogger` lands there.
- **Typed exits never cross a heterogeneous hook.** Spans and logs receive rendered strings,
  statuses, and exceptions — the boxing rule from the fiber-observer design.
- **Fable is unaffected.** Telemetry packages are .NET-only; core additions must compile under
  `FABLE_COMPILER` as no-ops or plain data.

## Piece 1: fix `Activity.trace` (span = execution)

Rebuild `trace` so the activity stops when the execution settles, and stamp the exit onto the
span before stopping:

- Fold over the execution (same `Execution.fold`/`tryExecution` machinery the runtime combinators
  use) rather than `use`-disposing in the construction lambda.
- On settle, before `activity.Stop()`:
  - `Exit.Success` → `ActivityStatusCode.Ok`.
  - `Cause.Fail e` → `ActivityStatusCode.Error`, tag `axial.flow.outcome = "fail"` and
    `axial.flow.error` with a rendered error (`string e` default; optional render parameter).
  - `Cause.Die exn` → `ActivityStatusCode.Error`, `axial.flow.outcome = "die"`, OpenTelemetry
    `exception.*` tags (reuse the `FiberTelemetry.tagDefect` conventions).
  - `Cause.Interrupt` → `axial.flow.outcome = "interrupt"` and status left unset (cancellation is
    not an error), plus `axial.flow.interrupted = true`.
  - Composite causes → outcome from the dominant branch (defect > fail > interrupt), full tree in
    `axial.flow.cause` via `Cause.prettyPrint`.
- Tag the current fiber id (`RuntimeState.current().FiberId` is internal; expose it via the
  existing `Flow.Runtime` surface, e.g. `Flow.Runtime.fiberId`) so spans and fiber telemetry
  correlate.
- `Activity.Current` restore must be correct across the await; spans must nest properly when
  `trace` wraps `trace`.

One canonical tag vocabulary, documented in the telemetry guide:
`axial.flow.outcome`, `axial.flow.error`, `axial.flow.cause`, `axial.flow.interrupted`,
`axial.flow.fiber.*` (already shipped), `axial.flow.annotation.*` (already shipped),
`exception.*` (OTel convention, already shipped).

## Piece 2: composable annotation sinks

- `Flow.withAnnotationSink` keeps replace semantics (it is `EditorBrowsable(Never)`
  integration plumbing) but `RuntimeContext` gains a compose story: a new
  `Flow.addAnnotationSink` (or `RuntimeContext.withComposedSink`) that tees to the existing sink
  before the new one. `Activity.trace` switches to the composing form so nested traces and
  user sinks all receive annotations.
- Sink exceptions are swallowed, matching the fiber-observer contract (today a throwing sink can
  fail the workflow at the `annotate` call site).
- Document the delivery semantics: a sink sees annotations set *after* installation; pre-existing
  annotations are replayed only by `Activity.trace`'s explicit replay, which stays.

## Piece 3: fiber spans with real duration and parentage

Use the observer's bracket to give each forked fiber a true span:

- In `Axial.Flow.Telemetry`, extend `FiberTelemetry.observer`: `OnStart` opens an
  `axial.flow.fiber` activity (parent = `Activity.Current` at the fork site, which is the correct
  causal parent because `Flow.fork` runs `OnStart` synchronously in the forking context) and
  stores it in a `ConditionalWeakTable<FiberMetadata, Activity>`; `OnEnd` looks it up, applies the
  Piece-1 exit conventions (status from `FiberStatus`, defect tags), and stops it.
- `OnUnobservedDefect` keeps its dedicated `axial.flow.fiber.unobserved_defect` span, now
  linked (span link or parent) to the fiber span when metadata is present.
- Make span-per-fiber opt-in (`FiberTelemetry.observeWithSpans` or an options record) — a
  hot loop forking thousands of fibers should be able to keep defect-only reporting.

## Piece 4: open the identity-trait trio

Add one extensible trait alongside the existing three (which remain and keep working):

```fsharp
type IHasTelemetryTags =
    abstract TelemetryTags: (string * string) list
```

`Activity.trace` (and fiber spans, at fork) apply these after the built-in trio. This closes the
"my environment has a session id" gap without growing a trait per identity concept and without
reflection over arbitrary environment shapes.

## Piece 5: logging meets the runtime

- **Give `ILog` an exception path.** Add `LogError: exn -> string -> unit`-shaped overload(s) to
  `ILog` (or a sibling interface to avoid breaking implementors — decide during implementation;
  the service is young enough that a direct break is probably fine pre-1.0). The MEL bridge in
  Hosting forwards the exception to `ILogger` properly.
- **Ship the fiber-observer logging in code.** `Axial.Flow.Hosting` gains
  `FiberLogging.observer : ILogger -> FiberObserver` (defects → `LogError`, unobserved defects →
  `LogCritical`) replacing the docs-recipe-only status, plus a `composeObserver` helper so
  logging and telemetry observers stack (`FiberObserver` composition: run both hooks, each
  guarded).
- **Delete `LogEntry` or use it.** Nothing constructs it. Either remove the type (pre-1.0
  cleanup) or make `Log.fromSink` accept `LogEntry -> unit` and have the runtime construct
  entries with timestamps from `IClock`. Removal is the default recommendation; revisit if a
  structured-log consumer appears.

## Piece 6: produce `Cause.Traced`

Add the missing producer so the existing cause-trace channel is reachable:

- `Flow.tracedError (trace: string) (flow: ...)` — on failure, wraps the cause in
  `Cause.Traced(cause, trace)`; success unchanged. Optionally an automatic variant where
  `Activity.trace` attaches its span name.
- `Cause.prettyPrint` output of traced trees lands in the `axial.flow.cause` tag (Piece 1), which
  is what makes the channel worth producing.

## Piece 7: tests (closes the §7 test item)

- Span lifetime: an async flow sleeping N ms produces a span with duration ≥ N (the Piece-1
  regression test).
- Exit mapping: one test per outcome (success/fail/die/interrupt/composite) asserting status and
  tags.
- Nesting: `trace` inside `trace` — parentage correct, both spans receive annotations set in the
  inner region (Piece-2 regression test).
- Propagation: annotations through fibers (fork inherits), layers, resources
  (finalizer-time annotations), `retry`, and `supervise` (each attempt's annotations reach the
  sink; attempt scoping doesn't leak).
- Fiber spans: fork inside a traced region → fiber span parented to the workflow span; duration
  covers fork→settle; defect fiber carries exception tags.
- Logging: `FiberLogging.observer` writes through a captured `ILogger`; composed observers both
  fire; a throwing logger cannot alter workflow outcomes.

## Non-goals

- **Metrics primitives** (counters/gauges/histograms) — `LATER_TODO.md` §14, separate work.
- **Source-location tracing model** — §14.
- **OpenTelemetry SDK dependency** — Telemetry stays on `System.Diagnostics.DiagnosticSource`;
  OTel exporters attach via the standard listener mechanisms.
- **Fable telemetry** — annotations and observer remain the cross-platform diagnostics surface;
  Activity/MEL are .NET-only by design.
- **Telemetry service contracts** (`IHas<ITelemetry>`-style) — §7a explicitly defers deciding
  whether telemetry becomes an explicit service; this work keeps it runtime instrumentation and
  does not preempt that decision.

## Sequencing

Piece 1 is the anchor and the only defect fix — it can ship alone and immediately makes
`Activity.trace` honest. Piece 2 rides with it (Piece 1's nesting test needs it). Pieces 3–4 are
additive telemetry features; Piece 5 is Hosting-side and independent; Piece 6 is small and can
tag along with Piece 1's cause tag. Piece 7 lands incrementally with each piece. Schema remains
the overall 1.0 priority; within Flow work, this ranks after nothing — it is the current §7 gap.
