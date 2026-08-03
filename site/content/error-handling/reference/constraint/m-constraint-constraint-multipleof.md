---
title: "Constraint.multipleOf"
linkTitle: "multipleOf"
weight: 2700
type: docs
---

Requires an exact multiple of the supplied divisor, under the value type&#39;s own arithmetic.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.multipleOf&#32;<span>divisor</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `divisor` | <code>^value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^value&gt;</span></code> |  |

## Remarks


 IEEE remainders are not the mathematical ones: <code>0.3 % 0.1</code> is not zero, so a float rule rejects values
 a mathematical reading accepts. Exporters therefore lower only integral and decimal divisors.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">batchSize</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">int</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">multipleOf</span> <span class="n">10</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val batchSize: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L670-670)
