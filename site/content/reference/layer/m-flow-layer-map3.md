---
title: "Flow.Layer.map3"
linkTitle: "map3"
weight: 2215
type: docs
---

Combines three layers with a mapping function.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.map3&#32;<span>mapper&#32;left&#32;middle&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'left&#32;->&#32;'middle&#32;->&#32;'right&#32;->&#32;'output</span></code> |  |
| `left` | <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'left</span>&gt;</span></code> |  |
| `middle` | <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'middle</span>&gt;</span></code> |  |
| `right` | <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'right</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
