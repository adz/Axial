---
title: "Constraint.Renderer.attributeName"
linkTitle: "attributeName"
weight: 2915
---

The attribute noun this renderer composes into a full message.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.attributeName&#32;<span>renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks


 Resolves <code>attribute.*</code> resources from most to least specific, then humanizes the final raw attribute
 segment. With no attribute it resolves the contextual <code>constraint.attribute.default</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">signup</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">attribute</span> <span class="s">&quot;postcodeID&quot;</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">attributeName</span> <span class="c">// &quot;Postcode ID&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L632-632)
