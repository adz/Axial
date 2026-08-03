---
title: "Constraint.lessThan"
linkTitle: "lessThan"
weight: 2603
---

Requires a value strictly less than the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.lessThan&#32;<span>maximum</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">discount</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">decimal</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">lessThan</span> <span class="n">1.0M</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val discount: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val decimal: value: &#39;T -&gt; decimal (requires member op_Explicit)<br /><br />--------------------<br />type decimal = System.Decimal<br /><br />--------------------<br />type decimal&lt;&#39;Measure&gt; =
  decimal</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L555-555)
