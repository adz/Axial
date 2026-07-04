---
title: "Flow.Flow.interrupt"
linkTitle: "interrupt"
weight: 2102
type: docs
---

Signals a fiber to stop and waits for it to finish its cleanup.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.interrupt&#32;<span>fiber</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fiber` | <code><span><a href="../../fiber/t-flow-fiber.md">Fiber</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> | The fiber to interrupt. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;<span><a href="../../exit/t-flow-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span>&gt;</span></code> | A flow that completes with the fiber&#39;s final outcome after interruption. |

## Remarks


 Interruption requests cooperative cancellation through the fiber&#39;s cancellation
 source and then waits for the child operation to report its final
 <a href="https://learn.microsoft.com/dotnet/api/axial.exit-2">Exit</a>.
