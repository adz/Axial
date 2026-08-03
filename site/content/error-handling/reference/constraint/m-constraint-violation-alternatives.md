---
title: "Constraint.Violation.alternatives"
linkTitle: "alternatives"
weight: 3111
type: docs
---


 Groups failures as rejected alternatives, returning <code>None</code> for no failures and the single failure
 unchanged for one.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Violation.alternatives&#32;<span>violations</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `violations` | <code><span><a href="../result/errors/t-constraint-violation.md">Violation</a>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../result/errors/t-constraint-violation.md">Violation</a>&#32;option</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="pn">[</span> <span class="id">first</span><span class="pn">;</span> <span class="id">second</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">alternatives</span> <span class="c">// Some (Any (first, [ second ]))</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L101-101)
