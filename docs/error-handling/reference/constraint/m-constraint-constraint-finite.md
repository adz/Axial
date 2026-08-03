---
title: "Constraint.finite"
linkTitle: "finite"
weight: 2701
---

Requires a double to be neither infinite nor <code>NaN</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.finite&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;float&gt;</span></code> |  |

## Remarks

<code>NaN</code> compares false against every value including itself, which silently corrupts sorting and makes a
 value unusable as a dictionary key. Excluding it is what makes ordering lawful.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">ratio</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">float</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">finite</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val ratio: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val float: value: &#39;T -&gt; float (requires member op_Explicit)<br /><br />--------------------<br />type float = System.Double<br /><br />--------------------<br />type float&lt;&#39;Measure&gt; =
  float</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L981-981)
