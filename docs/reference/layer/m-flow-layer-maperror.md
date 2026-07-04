---
title: "Flow.Layer.mapError"
linkTitle: "mapError"
weight: 2208
---

Maps the typed provisioning failure of a layer.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.mapError&#32;<span>mapper&#32;layer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error&#32;->&#32;'nextError</span></code> |  |
| `layer` | <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-layer.md">Layer</a>&lt;<span>'input,&#32;'nextError,&#32;'output</span>&gt;</span></code> |  |
