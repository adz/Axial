---
title: "Constraint.Violation.message"
linkTitle: "message"
weight: 3101
---

Renders a violation as a localized predicate, with no attribute noun.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.message&#32;<span>renderer&#32;violation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `renderer` | <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |
| `violation` | <code><a href="../result/errors/t-constraint-violation.md">Violation</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks


 Use this where a label already names the field — a form row, or a Schema result whose returned path
 identifies it. Conjunctions and alternatives join through the contextual <code>constraint.group.*</code>
 patterns; an actual value, when the violation carries one, is composed in through
 <code>constraint.actual</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">violation</span> <span class="o">|&gt;</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">message</span> <span class="pn">(</span><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">context</span> <span class="s">&quot;signup&quot;</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">attribute</span> <span class="s">&quot;name&quot;</span><span class="pn">)</span>
 <span class="c">// &quot;must be at least 13, but was 11&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L250-250)
