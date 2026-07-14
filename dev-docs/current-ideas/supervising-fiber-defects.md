# Supervising Fiber Defects

> **Status: implemented (2026-07-14).** Shipped as `Flow.Runtime.supervise` + `SupervisePolicy`,
> `FiberMetadata.Observed` + `Flow.forkDetached`, `FiberObserver` + `Flow.withFiberObserver` with all three
> detection mechanisms, and `FiberTelemetry.observe`/`FiberTelemetry.observer` in `Axial.Flow.Telemetry`.
> User guide: `docs/flow/concurrency/supervision.md`; runnable example:
> `examples/Axial.Examples/SupervisionExample.fs`. Tracked in `LATER_TODO.md` §7/§10.

## Problem

`Flow.fork` returns a `Fiber` handle, and nothing stops the caller discarding it
(`|> ignore`, `let! _ =`). When a discarded fiber hits an unhandled exception,
the trap in `Platform.startFiber` (Platform.fs) converts it to
`Exit.Failure (Cause.Die _)` and returns it through the `ExitTask` — which
nobody is awaiting. No log, no telemetry, no restart.

It is worse than ordinary fire-and-forget. A raw faulted `Task` that is GC'd
unobserved at least fires `TaskScheduler.UnobservedTaskException`. Axial's
`ExitTask` never faults — the trap converts every exception into an `Exit`
*value*, so the underlying `Task<Exit<_,_>>` always completes successfully and
.NET's safety net never triggers. By making defects into data, Axial
accidentally defuses the platform's own last-resort diagnostic. The runtime
therefore owes a replacement.

Discarded fork handles are not the only leak. The runtime itself discards exits
in two combinators, and there the user has no handle at all — the exit is
unobservable even in principle:

- `raceExecution` (Platform.fs): `Task.WhenAny` returns the winner's exit and
  cancels the loser; the loser's exit is dropped. A loser that dies with a
  `Cause.Die` is silent.
- `timeoutExecution` (.NET branch): on timeout it cancels the running
  operation, awaits it under `let! _ = ... with _ -> ()`, and discards the
  exit. A defect in the timed-out branch is silently dropped.

(`zipParExecution` is fine — it folds both settled exits through its choose
function, so causes are preserved. The scope-finalizer path in Flow.fs and the
`Axial.Flow.Process` output-pump tasks deserve a quick audit during
implementation.)

## The model

Four rules, one per error-handling concern:

1. **Typed errors are untouched.** `Cause.Fail<'error>` stays strictly inside
   the `Flow<'env, 'error, 'value>` signature. Nothing here observes, reports,
   or retries typed errors; they are domain values, not diagnostics.
2. **Recovery is declarative and opt-in:** `Flow.supervise` restarts a flow
   that dies with `Cause.Die`, configured on the cold blueprint before forking.
3. **Observation is tracked:** every `Fiber` knows whether its outcome was ever
   consumed. **Joining is the opt-out** — a fiber whose exit someone awaited is
   that caller's business, and the runtime says nothing.
4. **Unobservable defects are reported:** when a defect exit can never be seen
   — a forked fiber whose outcome is never consumed, or a race/timeout loser
   the runtime itself discards — it is reported through ambient fiber-observer
   hooks, which default to no-ops. The runtime decides nothing on its own; an
   application wires hooks once at its edge.

Rules 3 and 4 resolve the objection that reporting "designs a default for the
case the user explicitly didn't handle." With observation tracking, the
semantics match the judgment .NET itself makes for unobserved task exceptions:
if you wanted the outcome, you joined; if you discarded a fiber that then died
of a *bug* (not a typed error), being told is the point. Deliberate
fire-and-forget remains expressible at the call site (`forkDetached`, below).

Everything stays zero-dependency (plain function hooks, no logging framework),
compiles identically under Fable, and threads through the existing internal
`RuntimeContext` (Core.fs) following the `AnnotationSink` precedent — no new
public runner entry points.

## Roadmap alignment

This design is the concrete shape of two `LATER_TODO.md` areas and should be
tracked there:

- **§10 Post-v1.0 Fiber Runtime — "supervision hooks for fiber
  start/end/failure/interruption":** the fiber-observer hooks below are exactly
  that item. Since the threading work (context field, `startFiber` callback,
  fork propagation) is identical for one hook or three, the full §10 hook set
  ships together rather than a defect-only hook now and a breaking rework
  later. §10's "structured fiber dumps and richer runtime diagnostics" is
  *enabled* by the observed flag and existing parent/child `FiberMetadata` but
  not built here; §10's fiber-local state (FiberRef) and runtime flags are
  unrelated and stay untouched.
- **§7 v1.0 Observability:** the two open items — production-ready
  `ActivitySource` wrapper with "exit tagging, typed-error and defect
  recording, cancellation tagging, activity status, and available
  fiber/runtime metadata," and "integrate observability with
  `Axial.Flow.Telemetry` and `Microsoft.Extensions.Logging`" — are the
  consumer of these hooks. `Flow.Telemetry` already wires `AnnotationSink`; it
  gains the observer wiring the same way, which is how defect recording and
  fiber status reach spans without core taking any dependency. §7's remaining
  test item extends naturally: annotation/span propagation tests should cover
  `supervise` restarts alongside retries.

`Flow.supervise` itself is new — it appears nowhere in `LATER_TODO.md` and
should be added under §10 when this lands there. §10 being post-v1.0 matches
the sequencing below.

## Piece 1: `Flow.supervise` — active recovery

A combinator that re-evaluates a cold flow when it terminates with `Cause.Die`.
It is the defect-channel sibling of `Flow.retry`:

- `Flow.retry` re-runs on typed `Cause.Fail` errors and never touches defects.
- `Flow.supervise` re-runs on `Cause.Die` defects and never touches typed errors.

Shape mirrors `RetryPolicy<'error>` (Core.fs) so users learn one vocabulary:

```fsharp
type SupervisePolicy =
    {
      MaxAttempts: int
      Delay: int -> TimeSpan
      ShouldRestart: exn -> bool
    }

module Flow =
    let supervise (policy: SupervisePolicy) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```

The two policy types are deliberately *not* unified (e.g. as `RetryPolicy<exn>`),
for two reasons. Vocabulary guards the channel boundary: `Flow.retry` documents
"defects and interruptions are not retried," and one policy type serving both
would blur the exact `Fail`/`Die` line the whole design rests on. And the
combinators differ behaviorally, not just in predicate type: `supervise` closes
a fresh child scope per attempt (below), while `retry` runs all attempts in the
same scope. Same shape, different resource semantics — do not "unify" them
later without noticing this.

Implementation is the same recursive `Execution.fold` loop as `Flow.retry`
(Flow.fs), matching `Cause.Die` instead of `Cause.Fail`. Pure recursion, no
platform threading — identical on Fable.

Requirements:

- **Fresh child scope per attempt.** Re-invoking a cold flow gives fresh
  control flow but the same enclosing `Scope`, so finalizers registered by a
  failed attempt would otherwise accumulate unreleased until the whole scope
  closes — N restarts leaking N acquisitions. Each attempt runs inside its own
  child scope (`Scope.AddChild` exists) that is closed before the next attempt.
  The child scope is the closest analogue to Erlang's fresh heap.
- **A ceiling, always.** `MaxAttempts` is mandatory; there is no
  unlimited-restart variant. On exhaustion the final `Cause.Die` propagates as
  the flow's exit, exactly like exhausted `Flow.retry` — which means a
  supervised, forked, discarded flow that runs out of restarts still reaches
  the reporting net below. Supervision reduces how often the net is needed; it
  does not replace it.
- **Honest restart semantics in the docs.** Re-evaluation only resets state
  that lives inside the flow. If `'env` holds mutable state the crashed attempt
  corrupted, restart does not heal it. Erlang promises clean restarts because
  the heap dies with the process; a monadic re-invocation cannot, and the
  documentation must say so rather than borrow OTP's guarantees. For the same
  reason, do not borrow OTP's `Permanent`/`Transient`/`Temporary` vocabulary —
  those mean "when to restart," not retry counts, and reusing the words with
  different meanings would confuse exactly the audience this targets.

## Piece 2: observation tracking

`Fiber` gains an internal observed flag (plain mutable bool alongside the
existing mutable `FiberMetadata.Status`):

- Set by `Flow.join` and `Flow.interrupt` — any path that consumes the exit.
- Set at birth by a new `Flow.forkDetached`, the explicit fire-and-forget: "I
  know this can die and I don't want to hear about it." This is the call-site
  opt-out, stated in the code where the decision was made.
- A raw `|> ignore` of a `Flow.fork` handle leaves the flag unset — which is
  the point.

## Piece 3: fiber-observer hooks — passive reporting

The `LATER_TODO.md` §10 supervision-hook surface, threaded like
`AnnotationSink`. Add to the internal `RuntimeContext`:

```fsharp
type FiberObserver =
    {
      /// A fiber was forked. Receives the child's metadata (id, parent, start time).
      OnStart: FiberMetadata -> unit
      /// A fiber settled. Status on the metadata distinguishes
      /// Succeeded / Failed / Interrupted; a Cause.Die defect is passed alongside.
      OnEnd: FiberMetadata -> exn option -> unit
      /// A Cause.Die exit became unobservable: an unobserved fork reached
      /// finality, or a race/timeout loser was discarded by the runtime.
      OnUnobservedDefect: FiberMetadata option -> exn -> unit
    }

module FiberObserver =
    let none = { OnStart = ...ignore...; OnEnd = ...; OnUnobservedDefect = ... }

module Flow =
    let withFiberObserver (observer: FiberObserver) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```

Notes on the shape:

- A record is justified here where the earlier single-field `RuntimeServices`
  idea was not: §10 names start/end/failure/interruption as one surface, and
  the threading cost (context field, `startFiber` callback, fork propagation)
  is identical for one hook or three. Interruption needs no separate hook —
  `OnEnd` sees `FiberStatus.Interrupted` on the metadata.
- Hooks receive `FiberMetadata` and `exn`, never typed exits — a heterogeneous
  hook cannot take `Exit<'value, 'error>` without boxing to `obj`, which is the
  same type-erasure trap that killed the blueprint registry. Status plus
  optional defect covers every diagnostic need identified in §7.
- `OnUnobservedDefect` takes `FiberMetadata option` because race/timeout losers
  are executions, not fibers — there is no metadata to give.
- Defaults are no-ops; `Flow.fork` already propagates the parent context into
  children, so one edge-level `withFiberObserver` reaches every descendant
  fork. `Platform.fs` compiles before `Core.fs`, so `startFiber` takes plain
  callbacks rather than any context type.
- Exceptions thrown by hooks are swallowed. A diagnostics hook must never alter
  a fiber's exit or take down the runtime.

Semantics of `OnUnobservedDefect`:

- Fires only for exits containing `Cause.Die` (inspect the settled exit's
  cause, not just the exception trap — an `Execution` can carry a defect as
  data and return normally) that will *never* be observed.
- For forked fibers, fires when unobservedness becomes final (Piece 4), not at
  the moment of death. A fiber that dies and is joined later belongs to the
  joiner; one report, one meaning: "this defect escaped and no one will ever
  see it."
- For race/timeout losers, fires immediately at the discard site — the runtime
  knows at that moment that no observation is possible.
- At most one report per fiber, across all detection mechanisms.

Consumer: `Axial.Flow.Telemetry` wires the observer the same way it already
wires `AnnotationSink`, which is how §7's open items land — `OnEnd` gives exit
tagging, activity status, cancellation tagging, and fiber metadata on spans;
`OnUnobservedDefect` gives defect recording; an edge-level
`Microsoft.Extensions.Logging` recipe in the docs covers the logging
integration item. Core ships only the hooks.

## Piece 4: detecting "never observed"

Unobservedness is only knowable retroactively for forked fibers. Three
mechanisms, composed, all reporting through the same hook with a fired-once
guard:

**Immediate — runtime discard sites.** `raceExecution` and `timeoutExecution`
know at the discard point that the loser's exit can never be observed. If it
contains a non-interrupt `Cause.Die`, report right there. Deterministic,
immediate, no tracking needed.

**Deterministic — sweep at scope close.** `Flow.fork` runs inside the parent's
`RuntimeContext` and its `Scope`. The fork registers a lightweight check with
that scope (a `WeakReference` to the fiber metadata plus the observed flag);
when the scope closes, any child that settled with `Cause.Die` and was never
observed is reported. Defects surface deterministically at the structural
boundary they escaped — the structured-concurrency answer. Caveat: a handle
that escapes its forking scope and is joined afterward would be reported
spuriously; rare, and the weak reference keeps the sweep from extending any
lifetimes.

**Best-effort — GC-based.** A finalizer on a small sentinel the fiber holds
(.NET) / `FinalizationRegistry` (Fable — modern JS runtimes support it): if
collected with a `Die` exit and never observed, report. Unreachable means no
one can ever join, so this net has no false positives. It covers the gap the
sweep leaves — forks made near a long-lived root scope that only closes at
shutdown. Caveats: timing is nondeterministic, it may never fire if GC doesn't
run, and tests must force GC to exercise it. Same mechanism family as
`UnobservedTaskException` and Python's "coroutine was never awaited" warning.

## What we are not doing

- **No Erlang-style blueprint registry.** Making `Scope` hold cold `Flow`
  definitions so it can restart children forces heterogeneous flows to be boxed
  to `obj`, destroying the typed error channel. Rejected — and the same
  reasoning keeps typed exits out of the observer hooks.
- **No fail-fast scope cancellation.** `Scope` is a passive finalizer bag; it
  does not own cancellation, and a stray fork's crash silently cancelling
  sibling work is an application policy, not a runtime default. An application
  that wants fail-fast cancels its own root `CancellationTokenSource` inside
  its `OnUnobservedDefect` hook.
- **No always-fire defect hook.** An indiscriminate "every fiber defect" hook
  double-reports joined fibers and offers no call-site opt-out. Observation
  tracking subsumes it; anyone who wants every settled defect regardless of
  observation has it via `OnEnd`.
- **No fiber dumps, FiberRef, or runtime flags in this work.** The rest of §10
  stays in the backlog. The observed flag and existing parent/child metadata
  leave structured fiber dumps well-positioned for later.

## Sequencing

Schema remains the 1.0 priority; this is Flow-side work, picked up when Flow
work is next scheduled. `LATER_TODO.md` places the hook surface (§10)
post-v1.0, while §7's defect-recording and Telemetry/logging integration items
are v1.0-scoped observability — if §7 is pulled forward first, the hooks come
with it as its mechanism. Within the feature:

1. `Flow.supervise` — self-contained, pure, no runtime changes.
2. Observed flag + `forkDetached` — tiny, but must land before any reporting.
3. `FiberObserver` in `RuntimeContext` + immediate reporting at race/timeout
   discard sites + scope-close sweep — the deterministic core.
4. GC/`FinalizationRegistry` net — last; hardest to test, purely additive.
5. `Flow.Telemetry` observer wiring + `Microsoft.Extensions.Logging` recipe —
   discharges the two open §7 integration items; extend §7's propagation tests
   to cover `supervise` restarts.
