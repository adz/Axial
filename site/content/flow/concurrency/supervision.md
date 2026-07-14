---
weight: 15
title: Supervision and Fiber Observability
description: Restarting background work that dies with defects and observing fibers nobody awaits.
type: docs
---


A forked fiber whose handle is discarded can die silently.

`Flow.fork` returns a `Fiber` handle, and nothing stops a caller writing `|> Flow.map ignore` or `let! _ = ...` and dropping it. When such a fiber hits an unhandled exception, the runtime contains it as `Exit.Failure (Cause.Die _)` — but nobody is awaiting that exit. Because Axial converts every exception into an `Exit` *value*, the underlying task never faults, so even .NET's `TaskScheduler.UnobservedTaskException` net never fires. Without help, that is a production failure with no log line.

Axial answers this with two pieces: **`Flow.Runtime.supervise`** restarts background work that dies with defects, and the **fiber observer** reports the defects that still escape.

Both stay inside Axial's error model:

- **Typed errors (`Cause.Fail`) are untouched.** They are domain values in your `Flow<'env, 'error, 'value>` signature, not diagnostics. Supervision and observation apply only to *defects* (`Cause.Die`) — bugs that escaped the typed channel.
- **Joining is the opt-out.** A fiber whose outcome someone consumed (`Flow.join`, `Flow.interrupt`) belongs to that caller; the runtime says nothing about it.

## Restarting defects: `Flow.Runtime.supervise`

`supervise` is the defect-channel sibling of `Flow.Runtime.retry`:

- `retry` re-runs typed `Cause.Fail` errors and never touches defects.
- `supervise` re-runs `Cause.Die` defects and never touches typed errors or interruptions.

```fsharp
let reliableWorker =
    pollQueueForever
    |> Flow.Runtime.supervise
        { MaxAttempts = 5
          Delay = fun attempt -> TimeSpan.FromSeconds(float attempt)
          ShouldRestart = fun _ -> true }

let! fiber = Flow.fork reliableWorker
```

`SupervisePolicy` mirrors `RetryPolicy`: `MaxAttempts` bounds the restarts (there is deliberately no unlimited variant — a crash loop should eventually surface), `Delay` spaces the attempts, and `ShouldRestart` can inspect the defect exception. When attempts are exhausted, the final defect propagates as the flow's exit.

Two semantics worth knowing:

- **Each attempt runs in its own child scope.** Finalizers registered by a failed attempt run before the next attempt starts, so a supervised worker that acquires resources does not leak one acquisition per restart.
- **Restart is not an Erlang restart.** Re-evaluating the cold flow resets state that lives *inside* the flow. If your environment holds mutable state that the crashed attempt corrupted, restarting does not heal it.

## Deliberate fire-and-forget: `Flow.forkDetached`

If a background fiber's outcome genuinely does not matter, say so at the call site:

```fsharp
let! _fiber = Flow.forkDetached bestEffortCacheWarmup
```

A detached fiber counts as observed from birth, so a defect it dies with is never reported as unobserved. Use it instead of discarding a `Flow.fork` handle: a discarded `fork` handle whose fiber dies of a defect *is* reported.

## The safety net: `FiberObserver`

`FiberObserver` is a record of lifecycle hooks installed once at the application edge and carried implicitly to every descendant fork:

```fsharp
let observer =
    { FiberObserver.none with
        OnUnobservedDefect = fun metadata defect ->
            logger.LogError(defect, "Unobserved fiber defect (fiber {FiberId})",
                metadata |> Option.map (fun m -> m.Id.Value)) }

application
|> Flow.withFiberObserver observer
|> fun workflow -> workflow.RunSynchronously(env)
```

The hooks:

- `OnStart` — a fiber was forked; receives the child's `FiberMetadata`.
- `OnEnd` — a fiber settled; `FiberMetadata.Status` distinguishes `Succeeded`/`Failed`/`Interrupted`, and the defect exception (if the fiber died of one) is passed alongside. This fires for *every* fiber, observed or not — use it for metrics.
- `OnUnobservedDefect` — a defect became **unobservable**: a forked fiber died and nobody ever consumed its outcome, or the runtime itself discarded a `Flow.race` / timeout loser's exit (those never had a handle at all, so their metadata is `None`).

All hooks default to no-ops, receive diagnostic data only, and cannot alter any fiber's outcome — exceptions they throw are swallowed.

### When does "unobserved" fire?

Whether a fiber will ever be joined is only knowable retroactively, so the runtime reports at three moments:

1. **Immediately**, for race/timeout losers — the runtime knows at the discard site that no one can ever see that exit.
2. **When the forking scope closes**, for fibers that settled with a defect and were never observed — the deterministic, structured-concurrency boundary.
3. **When a discarded handle is garbage-collected**, as a best-effort net for forks made inside long-lived scopes (the same mechanism family as `UnobservedTaskException`; timing depends on GC).

Each defect is reported at most once, whichever mechanism gets there first.

Note the interaction with `supervise`: a supervised flow that exhausts its restarts still settles with `Cause.Die`, so a discarded supervised fiber still reaches the net. Supervision reduces how often the net is needed; it does not replace it.

## Telemetry integration

`Axial.Flow.Telemetry` ships a ready-made observer that records defects on the `Axial.Flow` activity source:

```fsharp
open Axial.Flow.Telemetry

application
|> FiberTelemetry.observe   // = Flow.withFiberObserver FiberTelemetry.observer
```

Every fiber that settles with a defect produces an `axial.flow.fiber.defect` error span, and every unobservable defect produces an `axial.flow.fiber.unobserved_defect` error span, tagged with fiber id, parent id, status, and OpenTelemetry-convention exception tags.

### Logging recipe

Wiring the observer to `Microsoft.Extensions.Logging` at the host edge:

```fsharp
let fiberLogging (logger: ILogger) =
    { FiberObserver.none with
        OnEnd = fun metadata defect ->
            match defect with
            | Some exn ->
                logger.LogError(exn, "Fiber {FiberId} died with a defect", metadata.Id.Value)
            | None -> ()
        OnUnobservedDefect = fun metadata defect ->
            logger.LogCritical(defect, "Unobserved fiber defect{FiberId}",
                metadata
                |> Option.map (fun m -> $" (fiber {m.Id.Value})")
                |> Option.defaultValue " (race/timeout loser)") }

application |> Flow.withFiberObserver (fiberLogging logger)
```

## Platform notes

Supervision and the observer hooks are pure F# and behave identically under Fable. The detection mechanisms differ slightly by platform: the scope-close sweep works everywhere; the GC net is .NET-only; and Fable's timeout cancels its loser without surfacing an exit, so timeout-loser reporting is .NET-only as well.
