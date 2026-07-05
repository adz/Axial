---
title: "Flow.Semaphore.withPermit"
linkTitle: "withPermit"
weight: 2103
type: docs
---

Runs a workflow while holding one permit and always releases the permit afterward.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Semaphore.withPermit&#32;<span>arg1&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code><a href="t-flow-flowsemaphore.md">FlowSemaphore</a></code> |  |
| `flow` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |
