---
title: "Constraint.optional"
linkTitle: "optional"
weight: 2302
---

Lifts a constraint over an optional container: absence passes, presence runs the inner constraint.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.optional&#32;<span>inner</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `inner` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^container&gt;</span></code> |  |

## Remarks


 Orthogonal to <code>present</code> and <code>blank</code>, which respectively require inhabitation and require absence.
 This one permits absence.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">nickname</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">option</span><span class="pn">&gt;</span> <span class="o">=</span>
     <span class="id">Constraint</span><span class="pn">.</span><span class="id">optional</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">lengthBetween</span> <span class="n">2</span> <span class="n">40</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val nickname: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>
<div popover class="fsdocs-tip" id="fs3">type &#39;T option = Option&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L472-472)
