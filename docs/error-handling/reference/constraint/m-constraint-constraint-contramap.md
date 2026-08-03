---
title: "Constraint.contramap"
linkTitle: "contramap"
weight: 2306
---

Applies a constraint to a projection of a larger value.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.contramap&#32;<span>project&#32;constraint'</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `project` | <code><span>'input&#32;->&#32;'value</span></code> |  |
| `constraint'` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'input&gt;</span></code> |  |

## Remarks


 Opaque: an arbitrary projection changes the proposition in a way no description can express. The inner
 description is retained beneath the opacity boundary so documentation stays readable.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">present</span> <span class="o">|&gt;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">contramap</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">order</span> <span class="k">-&gt;</span> <span class="id">order</span><span class="pn">.</span><span class="id">Reference</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L299-299)
