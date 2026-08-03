---
title: "Constraint.finite32"
linkTitle: "finite32"
weight: 2702
---

Requires a single-precision float to be neither infinite nor <code>NaN</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.finite32&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;float32&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">ratio</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">float32</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">finite32</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val ratio: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val float32: value: &#39;T -&gt; float32 (requires member op_Explicit)<br /><br />--------------------<br />type float32 = System.Single<br /><br />--------------------<br />type float32&lt;&#39;Measure&gt; =
  float32</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L986-986)
