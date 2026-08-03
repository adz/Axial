---
title: "Constraint.Renderer.context"
linkTitle: "context"
weight: 2911
---

Appends a document, model, form, or component segment.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.context&#32;<span>segment&#32;renderer</span></span></code>
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

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">signup</span> <span class="o">=</span> <span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">context</span> <span class="s">&quot;signup&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val signup: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L588-588)
