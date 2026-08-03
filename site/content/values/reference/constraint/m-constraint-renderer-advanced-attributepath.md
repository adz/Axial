---
title: "Constraint.Renderer.Advanced.attributePath"
linkTitle: "attributePath"
weight: 2919
type: docs
---

Sets the attribute to a complete path, replacing any previous one.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.Advanced.attributePath&#32;<span>segments&#32;renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `segments` | <code><span>string&#32;list</span></code> |  |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks

Schema supplies its typed <code>Path</code> keys through this; an empty list clears the attribute.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">attributePath</span> <span class="pn">[</span> <span class="s">&quot;address&quot;</span><span class="pn">;</span> <span class="s">&quot;postcode&quot;</span> <span class="pn">]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L705-705)
