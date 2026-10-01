---
title: Supervision and Fiber Observability
description: Restarting background work that dies with defects and observing fibers nobody awaits.
project: src/Axial.Telemetry/Axial.Telemetry.fsproj
---

# Supervision and Fiber Observability

A forked fiber whose handle is discarded can die silently.

`Flow.fork` returns a `Fiber` handle, and nothing stops a caller writing `|> Flow.map ignore` or `let! _ = ...` and dropping it. When such a fiber hits an unhandled exception, the runtime contains it as `Exit.Failure (Cause.Die _)`, but nobody is awaiting that exit. Because Axial converts every exception into an `Exit` *value*, the underlying task never faults, so even .NET's `TaskScheduler.UnobservedTaskException` net never fires. Without help, that is a production failure with no log line.

Axial answers this with two pieces: **`Flow.supervise`** restarts background work that dies with defects, and the **fiber observer** reports the defects that still escape.

Both stay inside Axial's error model:

- **Typed errors (`Cause.Fail`) are untouched.** They are domain values in your `Flow<'env, 'error, 'value>` signature, not diagnostics. Supervision and observation apply only to *defects* (`Cause.Die`): bugs that escaped the typed channel.
- **Joining is the opt-out.** A fiber whose outcome someone consumed (`Fiber.join`, `Fiber.interrupt`) belongs to that caller; the runtime says nothing about it.

## Restarting defects: `Flow.supervise`

`supervise` is the defect-channel sibling of `Flow.retry`:

- `retry` re-runs typed `Cause.Fail` errors and never touches defects.
- `supervise` re-runs `Cause.Die` defects and never touches typed errors or interruptions.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let polls = ref 0

/// Crashes on its first two runs, as a worker does on a poison message, then succeeds.
let pollQueue : Flow<ClockEnvironment, string, int> =
    Flow.delay (fun () ->
        let run = Interlocked.Increment &polls.contents
        if run <= 2 then Flow.die (InvalidOperationException "poison message") else Flow.ok run)

let restartPolicy =
    Retry.schedule
        { Retry.defaults with
            Retries = 4
            Backoff = Backoff.Exponential(TimeSpan.FromMilliseconds 1.0, TimeSpan.FromMilliseconds 10.0) }

let reliableWorker : Flow<ClockEnvironment, string, int> = pollQueue |> Flow.supervise restartPolicy
```

```fsharp run
flow {
    let! fiber = Flow.fork reliableWorker
    return! Fiber.join fiber
}
|> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Success 3)
```

`supervise` takes the same `Schedule` as `retry`, but its input is the defect exception, so `Schedule.whileInput` or a `Retry` record's `When` can decide which crashes are worth a restart. Bound the restarts with `Retries` or `Schedule.recursAtMost` (or a time budget with `Schedule.upTo`) so a crash loop eventually surfaces. When the schedule stops, the final defect propagates as the flow's exit.

Two semantics worth knowing:

- **Each attempt runs in its own child scope.** Finalizers registered by a failed attempt run before the next attempt starts, so a supervised worker that acquires resources does not leak one acquisition per restart. The successful attempt's scope stays open until the enclosing scope closes, so a value it returns is still usable.
- **Restart is not an Erlang restart.** Re-evaluating the cold flow resets state that lives *inside* the flow. If your environment holds mutable state that the crashed attempt corrupted, restarting does not heal it.

## Deliberate fire-and-forget: `Flow.forkDetached`

If a background fiber's outcome genuinely does not matter, say so at the call site:

```fsharp
let bestEffortCacheWarmup : Flow<ClockEnvironment, string, unit> = Flow.die (InvalidOperationException "cache warmup failed")

let startWarmup : Flow<ClockEnvironment, string, unit> =
    flow {
        let! _fiber = Flow.forkDetached bestEffortCacheWarmup
        return ()
    }
```

A detached fiber counts as observed from birth, so a defect it dies with is never reported as unobserved. Use it instead of discarding a `Flow.fork` handle: a discarded `fork` handle whose fiber dies of a defect *is* reported.

## The safety net: `FiberObserver`

`FiberObserver` is a record of lifecycle hooks installed once at the application edge and carried implicitly to every descendant fork:

```fsharp
let unobserved = ResizeArray<string>()

let observer =
    { FiberObserver.none with
        OnUnobservedDefect = fun _ defect -> lock unobserved (fun () -> unobserved.Add defect.Message) }

/// Detaches one failing fiber and discards the handle of another.
let application : Flow<ClockEnvironment, string, unit> =
    flow {
        do! startWarmup
        let! _ = Flow.fork (Flow.die (InvalidOperationException "lost order") : Flow<ClockEnvironment, string, unit>)
        do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
    }
    |> Flow.scoped
```

```fsharp run
application |> Flow.withFiberObserver observer |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success())
List.ofSeq unobserved |> shouldEqual [ "lost order" ]
```

The discarded fork's defect was reported when its scope closed. The detached warmup's was not.

The hooks:

- `OnStart`: a fiber was forked; receives the child's `FiberMetadata`.
- `OnEnd`: a fiber settled; `FiberMetadata.Status` distinguishes `Succeeded`/`Failed`/`Interrupted`, and the defect exception (if the fiber died of one) is passed alongside. This fires for *every* fiber, observed or not, so use it for metrics.
- `OnUnobservedDefect`: a defect became **unobservable**: a forked fiber died and nobody ever consumed its outcome, or the runtime itself discarded a `Flow.race` / timeout loser's exit (those never had a handle at all, so their metadata is `None`).

All hooks default to no-ops, receive diagnostic data only, and cannot alter any fiber's outcome; exceptions they throw are swallowed.

### When does "unobserved" fire?

Whether a fiber will ever be joined is only knowable retroactively, so the runtime reports at three moments:

1. **Immediately**, for race/timeout losers: the runtime knows at the discard site that no one can ever see that exit.
2. **When the forking scope closes**, for fibers that settled with a defect and were never observed. Scope close is a fixed point in the program, so the report arrives at a predictable time.
3. **When a discarded handle is garbage-collected**, as a best-effort net for forks made inside long-lived scopes (the same mechanism family as `UnobservedTaskException`; timing depends on GC).

Each defect is reported at most once, whichever mechanism gets there first.

Note the interaction with `supervise`: a supervised flow that exhausts its restarts still settles with `Cause.Die`, so a discarded supervised fiber still reaches the net. Supervision reduces how often the net is needed; it does not replace it.

## Telemetry integration

`Axial.Telemetry` ships a ready-made observer that records defects on the `Axial` activity source:

```fsharp
let tracedApplication : Flow<ClockEnvironment, string, unit> =
    application |> FiberTelemetry.observe // = Flow.withFiberObserver FiberTelemetry.observer
```

Every fiber that settles with a defect produces an `axial.flow.fiber.defect` error span, and every unobservable defect produces an `axial.flow.fiber.unobserved_defect` error span, tagged with fiber id, parent id, status, and OpenTelemetry-convention exception tags.

### Logging

`Axial.Hosting` ships the `Microsoft.Extensions.Logging` wiring: `FiberLogging.observe logger` logs
fiber defects as errors and unobserved defects as critical entries, with the exception attached. Observers
compose, so telemetry and logging stack from one edge install:

```fsharp no-check reason="Needs Axial.Hosting for Microsoft.Extensions.Logging as well as Axial.Telemetry; this page checks against Axial.Telemetry"
let observeWithLogging (logger: Microsoft.Extensions.Logging.ILogger) (workflow: Flow<ClockEnvironment, string, unit>) =
    workflow
    |> Flow.withFiberObserver (FiberObserver.compose FiberTelemetry.observer (FiberLogging.observer logger))
```

## Platform notes

Supervision and the observer hooks are pure F# and behave identically under Fable. The detection mechanisms differ slightly by platform: the scope-close sweep and timeout-loser reporting work everywhere, and the GC net is .NET-only.
