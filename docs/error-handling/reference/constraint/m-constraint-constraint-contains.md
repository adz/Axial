---
title: "Constraint.contains"
linkTitle: "contains"
weight: 2608
---

Requires a collection to contain the supplied item.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.contains&#32;<span>expected</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^container&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">mustIncludeAdmin</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">list</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">contains</span> <span class="s">&quot;admin&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val mustIncludeAdmin: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>
<div popover class="fsdocs-tip" id="fs3">type &#39;T list = List&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L918-918)
