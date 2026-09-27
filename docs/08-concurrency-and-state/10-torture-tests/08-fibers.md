---
title: Fibers
description: "Latest-wins fiber slots, context inherited by forked fibers, and the fiber registry and observers under a burst of named fibers."
---

# Fibers

This scenario covers the [fiber](../fibers.html) tools that decide which work keeps running, what forked work inherits,
and how a running program reports its fibers.

Run it with `dotnet run --project examples/Axial.TortureTest -- fibers 100`.

## What it does

- Sixty searches are forked into one `FiberSlot` with `Flow.forkReplacing`, so each replaces the one before it, as
  when a user keeps typing. The same runs per key with `FiberSlot.makeKeyed` and `Flow.forkReplacingKey`.
- Ten workers are forked under `Flow.annotate`, `Flow.withTraceId`, annotation sinks, and `Flow.localEnv`, and each
  reports what it inherited.
- Forty named fibers that succeed, fail, or sleep run under a `FiberRegistry` and a `FiberObserver`. The sleepers are
  interrupted through the registry by id and by name. A discarded fiber and a detached fiber both die.

{{< snippet id="torture-fibers" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| The last request's result is delivered | Replacing never interrupts the newest fiber. |
| Every replaced request finished or was interrupted, and none is left running | A slot holds one fiber, and interrupting the slot stops it. |
| Every request cleaned up | A replaced fiber runs its `Flow.ensuring` cleanup. |
| The last preview per key finished, and interruptAll left none running | Keyed slots apply the same rule per key. |
| Forked fibers inherit annotations, the trace id, and the environment, but not their siblings' annotations | A fork copies its parent's context; a sibling's annotation stays with that sibling. |
| Every fiber has its own id | `Flow.fiberId` identifies each fork. |
| Annotation sinks saw the annotations | `Flow.addAnnotationSink` composes with the sink outside it. |
| Interrupting by id and by name reached every sleeper | `FiberRegistry.Interrupt` and `InterruptByName` find live fibers. |
| The registry counted every named fiber and how each ended | `FiberRegistry.Stats` groups outcomes by fiber name. |
| Nothing is live afterwards, and history stays within its capacity | Settled fibers leave the live set, and the settled history is bounded. |
| The observer saw a dump of every start, and each renders | `FiberDump` renders what the observer saw. |
| A discarded fiber's defect was reported, a detached one's was not | A defect nobody observed is reported when its scope closes, unless the fiber was forked with `Flow.forkDetached`. |
| The observer saw every fiber start and end | Every start has a matching end. |
