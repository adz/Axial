---
title: "Flow.Semaphore.withPermit"
linkTitle: "withPermit"
weight: 2103
---

Runs a workflow while holding one permit and always releases the permit afterward.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Semaphore.withPermit&#32;<span>arg1&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code><a href="/reference/Axial/axial-flow-flowsemaphore.html">FlowSemaphore</a></code> |  |
| `flow` | <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |
