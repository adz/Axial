---
title: "Constraint.Renderer.withValues"
linkTitle: "withValues"
weight: 2914
---

Replaces all operand rendering with one callback, ignoring placeholder format suffixes.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.withValues&#32;<span>format&#32;renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `format` | <code><span><a href="t-constraint-constraintvalue.md">ConstraintValue</a>&#32;->&#32;string</span></code> |  |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks


 List operands still join through the contextual <code>constraint.list.*</code> patterns; the callback renders
 each item. Use <code>Renderer.Advanced.withValueFormatting</code> when the suffix matters.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">withValues</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">value</span> <span class="k">-&gt;</span> <span class="id">ConstraintValue</span><span class="pn">.</span><span class="id">render</span> <span class="id">value</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L620-620)
