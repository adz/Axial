---
title: "Layer.zipPar"
linkTitle: "zipPar"
weight: 2211
---

Builds two independent layers in parallel and returns both outputs.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.zipPar&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'left</span>&gt;</span></code> |  |
| `right` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'right</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;<span>(<span>'left&#32;*&#32;'right</span>)</span></span>&gt;</span></code> |  |

## Remarks


 Each branch is provisioned in a parent-owned child scope. When the parent scope closes,
 child scopes are closed in deterministic left-to-right order. If both branches fail,
 both failures are returned as a parallel cause.
