---
title: "Layer.mapError"
linkTitle: "mapError"
weight: 2204
type: docs
---

Maps the typed provisioning failure of a layer.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.mapError&#32;<span>mapper&#32;layer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error&#32;->&#32;'nextError</span></code> |  |
| `layer` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'nextError,&#32;'output</span>&gt;</span></code> |  |
