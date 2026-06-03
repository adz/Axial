---
title: "Flow.interrupt"
linkTitle: "interrupt"
weight: 2102
---

Signals a fiber to stop and waits for it to finish its cleanup.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.interrupt&#32;<span>fiber</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fiber` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-fiber-2.html">Fiber</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> | The fiber to interrupt. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'none,&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span>&gt;</span></code> | A flow that completes with the fiber&#39;s final outcome after interruption. |

## Remarks


 Interruption requests cooperative cancellation through the fiber&#39;s cancellation
 source and then waits for the child operation to report its final
 <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>.
