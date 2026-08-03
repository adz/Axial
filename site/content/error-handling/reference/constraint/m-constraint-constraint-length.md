---
title: "Constraint.length"
linkTitle: "length"
weight: 2402
type: docs
---

Requires text or a collection to have exactly the supplied size.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.length&#32;<span>expected</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^value&gt;</span></code> |  |

## Remarks

Text sizes count Unicode code points, so one emoji counts once even though it is two UTF-16 units.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">code</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">length</span> <span class="n">6</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val code: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L460-460)
