---
title: "Constraint.Renderer.attribute"
linkTitle: "attribute"
weight: 2912
type: docs
---

Replaces the attribute with one segment.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.attribute&#32;<span>segment&#32;renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `segment` | <code>string</code> |  |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks


 Replacement, not append: a form-scoped renderer stays reusable for sibling fields. Schema supplies its
 typed path through <code>Renderer.Advanced.attributePath</code> instead.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">signup</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">attribute</span> <span class="s">&quot;name&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L600-600)
