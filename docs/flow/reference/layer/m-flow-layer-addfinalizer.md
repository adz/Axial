---
title: "Flow.Layer.addFinalizer"
linkTitle: "addFinalizer"
weight: 2205
---

Registers an asynchronous finalizer with the layer scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.addFinalizer&#32;<span>finalizer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `finalizer` | <code><a href="/reference/Axial/axial-flow-platform-finalizer.html">Finalizer</a></code> | The finalizer to run when the layer scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;unit</span>&gt;</span></code> | A layer that registers the finalizer. |
