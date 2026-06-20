---
title: "Flow.Layer.addFinalizer"
linkTitle: "addFinalizer"
weight: 2205
type: docs
---

Registers an asynchronous finalizer with the layer scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.addFinalizer&#32;<span>finalizer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `finalizer` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The finalizer to run when the layer scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;unit</span>&gt;</span></code> | A layer that registers the finalizer. |
