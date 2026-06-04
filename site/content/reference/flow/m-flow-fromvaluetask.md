---
title: "Flow.fromValueTask"
linkTitle: "fromValueTask"
weight: 2310
type: docs
---

Creates a flow from a raw value task operation.

**Platform:** .NET only

## Signature

<div class="fsdocs-usage">
<code><span>Flow.fromValueTask&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |
