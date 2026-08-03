---
title: "Constraint.ConstraintAtom.key"
linkTitle: "key"
weight: 3003
type: docs
---

The stable message key for an atom, derived mechanically from its case.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.ConstraintAtom.key&#32;<span>atom</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `atom` | <code><a href="t-constraint-constraintatom.md">ConstraintAtom</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">ConstraintAtom</span><span class="pn">.</span><span class="id">key</span> <span class="pn">(</span><span class="id">RelationAtom</span> <span class="pn">(</span><span class="id">Compared</span> <span class="pn">(</span><span class="id">AtLeast</span><span class="pn">,</span> <span class="id">ConstraintValue</span><span class="pn">.</span><span class="id">Integer</span> <span class="n">3L</span><span class="pn">)</span><span class="pn">)</span><span class="pn">)</span>
 <span class="c">// &quot;constraint.relation.atLeast&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L149-149)
