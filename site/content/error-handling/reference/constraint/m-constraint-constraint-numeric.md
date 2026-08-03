---
title: "Constraint.numeric"
linkTitle: "numeric"
weight: 2502
type: docs
---

Requires text to be one or more ASCII digits.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.numeric&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;string&gt;</span></code> |  |

## Remarks


 ASCII rather than <code>\d</code>. .NET&#39;s <code>\d</code> matches any Unicode decimal digit while ECMA-262&#39;s matches
 <code>[0-9]</code>, so a Unicode rule could not be exported to JSON Schema without the exported schema rejecting
 values the library accepts.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">pin</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">numeric</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val pin: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L514-514)
