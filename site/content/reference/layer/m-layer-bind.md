---
title: "Layer.bind"
linkTitle: "bind"
weight: 2209
type: docs
---

Sequences layer provisioning with a dependent follow-up layer.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.bind&#32;<span>binder&#32;layer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'output&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'next</span>&gt;</span></span></code> |  |
| `layer` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'next</span>&gt;</span></code> |  |
