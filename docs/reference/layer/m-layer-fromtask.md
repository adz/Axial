---
title: "Layer.fromTask"
linkTitle: "fromTask"
weight: 2201
---

Creates a layer from a raw task provisioning function.

**Platform:** .NET only

## Signature

<div class="fsdocs-usage">
<code><span>Layer.fromTask&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><span>'input&#32;*&#32;<a href="../scope/t-scope.md">Scope</a></span>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<span><a href="../exit/t-exit.md">Exit</a>&lt;<span>'output,&#32;'error</span>&gt;</span>&gt;</span></span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
