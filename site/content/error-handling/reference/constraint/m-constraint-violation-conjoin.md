---
title: "Constraint.Violation.conjoin"
linkTitle: "conjoin"
weight: 2907
type: docs
---


 Groups failures as a conjunction, returning <code>None</code> for no failures and the single failure unchanged
 for one.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.conjoin&#32;<span>violations</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `violations` | <code><span><a href="../result/errors/t-constraint-violation.md">Violation</a>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../result/errors/t-constraint-violation.md">Violation</a>&#32;option</span></code> |  |

## Remarks


 This is the normalization Axial itself applies, so an interpreter that accumulates failures from several
 constraints produces exactly the tree a single composed constraint would have produced. Axial-produced
 groups are therefore never empty and never unary.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="pn">[</span> <span class="id">first</span><span class="pn">;</span> <span class="id">second</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">conjoin</span> <span class="c">// Some (All (first, [ second ]))</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L97-97)
