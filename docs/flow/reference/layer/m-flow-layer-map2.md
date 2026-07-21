---
title: "Flow.Layer.map2"
linkTitle: "map2"
weight: 2213
---

Combines two layers with a mapping function.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.map2&#32;<span>mapper&#32;left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'left&#32;->&#32;'right&#32;->&#32;'output</span></code> |  |
| `left` | <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'left</span>&gt;</span></code> |  |
| `right` | <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'right</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
