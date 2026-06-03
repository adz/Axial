---
title: "Semaphore.withPermit"
linkTitle: "withPermit"
weight: 2103
---

Runs a workflow while holding one permit and always releases the permit afterward.

## Signature

<div class="fsdocs-usage">
<code><span>Semaphore.withPermit&#32;<span>arg1&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flowsemaphore.html">FlowSemaphore</a></code> |  |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |
