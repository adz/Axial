---
title: "Constraint.Renderer.ofLookup"
linkTitle: "ofLookup"
weight: 2907
type: docs
---

A renderer backed by any key-to-template lookup.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.ofLookup&#32;<span>lookup</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `lookup` | <code><a href="t-constraint-messagelookup.md">MessageLookup</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks

The portable constructor, and the one Fable applications use.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">renderer</span> <span class="o">=</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">ofLookup</span> <span class="id">translations</span><span class="pn">.</span><span class="id">TryFind</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val renderer: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L503-503)
