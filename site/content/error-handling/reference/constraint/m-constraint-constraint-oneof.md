---
title: "Constraint.oneOf"
linkTitle: "oneOf"
weight: 2607
type: docs
---

Requires the value to equal one of the supplied choices.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.oneOf&#32;<span>choices</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `choices` | <code><span>'value&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">currency</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">oneOf</span> <span class="pn">[</span> <span class="s">&quot;AUD&quot;</span><span class="pn">;</span> <span class="s">&quot;NZD&quot;</span> <span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val currency: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L764-764)
