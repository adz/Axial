---
title: "Flow.Flow.fromTask"
linkTitle: "fromTask"
weight: 2310
---

Creates a flow from a raw task operation.

**Platform:** .NET only

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.fromTask&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Remarks

Thrown exceptions are recorded as defects (<code>Cause.Die</code>), while cancellation is recorded as interruption. Use <code>attemptTask</code> when expected exceptions should enter the typed error channel.
