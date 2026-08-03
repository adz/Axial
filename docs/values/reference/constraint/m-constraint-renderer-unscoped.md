---
title: "Constraint.Renderer.unscoped"
linkTitle: "unscoped"
weight: 2913
---

Clears both the context and the attribute.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.unscoped&#32;<span>renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">bare</span> <span class="o">=</span> <span class="id">signup</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">unscoped</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val bare: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L610-610)
