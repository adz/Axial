---
title: "Constraint.lengthBetween"
linkTitle: "lengthBetween"
weight: 2405
---

Requires a text or collection size inside the supplied inclusive bounds.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.lengthBetween&#32;<span>minimum&#32;maximum</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |
| `maximum` | <code>int</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^value&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">name</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">lengthBetween</span> <span class="n">2</span> <span class="n">40</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val name: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L564-564)
