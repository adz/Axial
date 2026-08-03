---
title: "Constraint.ConstraintValue.tryCreate"
linkTitle: "tryCreate"
weight: 3006
type: docs
---


 Projects a runtime value to its portable representation, or <code>None</code> when the type is outside the closed
 set. This never throws, including for <code>NaN</code>, infinities, and values no numeric case can hold.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.ConstraintValue.tryCreate&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraintvalue.md">ConstraintValue</a>&#32;option</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">ConstraintValue</span><span class="pn">.</span><span class="id">tryCreate</span> <span class="n">3</span> <span class="o">=</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Some</span> <span class="pn">(</span><span class="id">ConstraintValue</span><span class="pn">.</span><span class="id">Integer</span> <span class="n">3L</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintValue.fs#L159-159)
