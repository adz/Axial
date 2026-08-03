---
title: "Constraint.greaterThan"
linkTitle: "greaterThan"
weight: 2602
type: docs
---

Requires a value strictly greater than the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.greaterThan&#32;<span>minimum</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">quantity</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">int</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">greaterThan</span> <span class="n">0</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val quantity: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L550-550)
