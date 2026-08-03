---
title: "Constraint.Violation.renderWith"
linkTitle: "renderWith"
weight: 3103
---


 Renders a violation through a caller-supplied lookup, keeping the same grouping and separators
 <code>render</code> uses.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.renderWith&#32;<span>lookup&#32;violation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `lookup` | <code><span><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a>&#32;->&#32;string</span></code> |  |
| `violation` | <code><a href="../result/errors/t-constraint-violation.md">Violation</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks


 The whole localization path in one call. <code>toMessageTree</code> remains available for a translator that
 needs to control word order across a group; this is for the common case, where a resource lookup per
 message is the entire job and matching the tree by hand is pure ceremony. Verbatim leaves — author prose
 with no catalogue key — are passed through untranslated, because there is nothing to look up.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">violation</span> <span class="o">|&gt;</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">renderWith</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">descriptor</span> <span class="k">-&gt;</span> <span class="id">resources</span><span class="pn">.</span><span class="id">Format</span><span class="pn">(</span><span class="id">descriptor</span><span class="pn">.</span><span class="id">Key</span><span class="pn">,</span> <span class="id">descriptor</span><span class="pn">.</span><span class="id">Arguments</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L214-214)
