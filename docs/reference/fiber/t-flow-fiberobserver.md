---
title: "Flow.FiberObserver"
linkTitle: "FiberObserver"
weight: 1005
---


 Runtime hooks observing fiber lifecycle events for diagnostics and telemetry.


## Signature

<div class="fsdocs-usage">
<code>type FiberObserver</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `OnStart` | A fiber was forked. Receives the child fiber's metadata. |
| `OnEnd` |
 A fiber settled. <code>FiberMetadata.Status</code> distinguishes success, failure, and interruption; the
 first <code>Cause.Die</code> defect in the exit, if any, is passed alongside.
  |
| `OnUnobservedDefect` |
 A <code>Cause.Die</code> defect became unobservable: a forked fiber died unobserved and no observation can
 happen anymore, or the runtime discarded a race/timeout loser&#39;s exit. The metadata is absent for
 discarded race/timeout losers, which are executions rather than fibers.
  |

## Remarks


 Installed once at the application edge with <code>Flow.withFiberObserver</code> and carried implicitly to every
 descendant fork. All hooks default to no-ops, receive only diagnostic data (<code>FiberMetadata</code> and defect
 exceptions, never typed exits), and must not throw; exceptions raised by hooks are swallowed so a
 diagnostics hook can never alter a fiber&#39;s outcome.
