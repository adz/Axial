---
title: "Constraint.equalTo"
linkTitle: "equalTo"
weight: 2600
type: docs
---

Requires equality with the supplied value, under F# structural equality.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.equalTo&#32;<span>expected</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">mustBeDraft</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span class="id">Status</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">equalTo</span> <span class="id">Status</span><span class="pn">.</span><span class="id">Draft</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val mustBeDraft: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L716-716)
