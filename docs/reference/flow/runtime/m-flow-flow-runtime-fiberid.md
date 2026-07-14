---
title: "Flow.Runtime.fiberId"
linkTitle: "fiberId"
weight: 2113
---

Reads the current fiber id from the ambient runtime context.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.fiberId&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;<a href="../../fiber/t-flow-fiberid.md">FiberId</a></span>&gt;</span></code> | A flow that succeeds with the current <a href="../../fiber/t-flow-fiberid.md">FiberId</a>. |

## Remarks


 The root workflow runs on a fiber id of its own; every <code>Flow.fork</code> child gets a fresh id.
 Telemetry integrations use this to correlate workflow spans with fiber lifecycle events.
