---
title: "Constraint.minLength"
linkTitle: "minLength"
weight: 2403
type: docs
---

Requires text or a collection to have at least the supplied size.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.minLength&#32;<span>minimum</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;^value&gt;</span></code> |  |

## Remarks

Literal size, so a single space satisfies <code>minLength 1</code>. Use <code>present</code> to reject whitespace.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">tags</span> <span class="pn">:</span> <span class="id">Constraint</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">list</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">minLength</span> <span class="n">1</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val tags: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>
<div popover class="fsdocs-tip" id="fs3">type &#39;T list = List&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L466-466)
