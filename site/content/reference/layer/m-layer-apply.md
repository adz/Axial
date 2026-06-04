---
title: "Layer.apply"
linkTitle: "apply"
weight: 2214
type: docs
---

Applies a layer-wrapped function to a layer-wrapped value.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.apply&#32;<span>layer&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `layer` | <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;<span>(<span>'value&#32;->&#32;'next</span>)</span></span>&gt;</span></code> |  |
| `value` | <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'next</span>&gt;</span></code> |  |
