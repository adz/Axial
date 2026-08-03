---
title: "Constraint.Violation.toMessageTree"
linkTitle: "toMessageTree"
weight: 3104
---


 Projects a violation for an external localization system, preserving its grouping so a translator renders
 conjunctions and alternatives in their own word order.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.toMessageTree&#32;<span>violation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `violation` | <code><a href="t-constraint-violation.md">Violation</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-messagetree.md">MessageTree</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">match</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">toMessageTree</span> <span class="id">violation</span> <span class="k">with</span>
 <span class="pn">|</span> <span class="id">MessageTree</span><span class="pn">.</span><span class="id">Leaf</span> <span class="pn">(</span><span class="id">MessageLeaf</span><span class="pn">.</span><span class="id">Localized</span> <span class="id">descriptor</span><span class="pn">)</span> <span class="k">-&gt;</span> <span class="id">MessageDescriptor</span><span class="pn">.</span><span class="id">key</span> <span class="id">descriptor</span>
 <span class="pn">|</span> <span class="id">_</span> <span class="k">-&gt;</span> <span class="s">&quot;constraint.group&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L179-179)
