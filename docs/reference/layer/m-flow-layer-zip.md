---
title: "Flow.Layer.zip"
linkTitle: "zip"
weight: 2210
---

Builds two layers from the same input and scope and returns both outputs.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.zip&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'left</span>&gt;</span></code> |  |
| `right` | <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'right</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;<span>(<span>'left&#32;*&#32;'right</span>)</span></span>&gt;</span></code> |  |

## Remarks

<code>zip</code> is sequential: the left layer is provisioned before the right layer.
 Use <code>zipPar</code> or <code>merge</code> for independent parallel provisioning.
