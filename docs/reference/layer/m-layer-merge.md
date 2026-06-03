---
title: "Layer.merge"
linkTitle: "merge"
weight: 2208
---

Merges two independent service layers in parallel.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.merge&#32;<span>left&#32;right</span></span></code>
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

<code>merge</code> is the layer-domain name for <code>zipPar</code>. Use it when combining
 service bundles or environment fragments that do not depend on each other.
