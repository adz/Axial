---
title: "Layer.map2"
linkTitle: "map2"
weight: 2209
---

Combines two layers with a mapping function.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.map2&#32;<span>mapper&#32;left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'left&#32;->&#32;'right&#32;->&#32;'output</span></code> |  |
| `left` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'left</span>&gt;</span></code> |  |
| `right` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'right</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
