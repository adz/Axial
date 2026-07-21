---
title: "Flow.Layer.map"
linkTitle: "map"
weight: 2207
---

Maps the successful output of a layer.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.map&#32;<span>mapper&#32;layer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'output&#32;->&#32;'next</span></code> |  |
| `layer` | <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'next</span>&gt;</span></code> |  |
