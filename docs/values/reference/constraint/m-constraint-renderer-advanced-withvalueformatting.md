---
title: "Constraint.Renderer.Advanced.withValueFormatting"
linkTitle: "withValueFormatting"
weight: 2918
---

Replaces operand formatting with a callback that receives the placeholder&#39;s format suffix.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.Advanced.withValueFormatting&#32;<span>format&#32;renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `format` | <code><span><a href="t-constraint-valueformatrequest.md">ValueFormatRequest</a>&#32;->&#32;string</span></code> |  |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">withValueFormatting</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">request</span> <span class="k">-&gt;</span> <span class="id">myFormat</span> <span class="id">request</span><span class="pn">.</span><span class="id">Value</span> <span class="id">request</span><span class="pn">.</span><span class="id">Format</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L696-696)
