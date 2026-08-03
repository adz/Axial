---
title: "Constraint.between"
linkTitle: "between"
weight: 2606
---

Requires a value inside the supplied inclusive bounds.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.between&#32;<span>minimum&#32;maximum</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>'value</code> |  |
| `maximum` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">retryCount</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">int</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">between</span> <span class="n">0</span> <span class="n">10</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val retryCount: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L570-570)
