---
title: "Constraint.blank"
linkTitle: "blank"
weight: 2401
type: docs
---

Requires a value to be uninhabited according to its shape; the exact complement of <code>present</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.blank&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^value&gt;</span></code> |  |

## Remarks


 This <em>requires</em> absence. To permit it, use <code>Constraint.optional</code>; to allow a property to be
 omitted from an input, use Schema&#39;s <code>mayOmit</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">mustBeUnset</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">blank</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val mustBeUnset: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L390-390)
