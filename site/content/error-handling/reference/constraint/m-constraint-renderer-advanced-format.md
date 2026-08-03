---
title: "Constraint.Renderer.Advanced.format"
linkTitle: "format"
weight: 2923
type: docs
---

Renders any catalogue&#39;s entry through the full contextual, plural, and formatting path.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.Advanced.format&#32;<span>spec&#32;renderer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `spec` | <code><a href="t-constraint-messageformatspec.md">MessageFormatSpec</a></code> |  |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks


 The entry point another package&#39;s catalogue uses. Schema renders its <code>schema.*</code> entries with this
 and adds no Schema knowledge to <code>Axial.Constraint</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">format</span> <span class="id">spec</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L764-764)
