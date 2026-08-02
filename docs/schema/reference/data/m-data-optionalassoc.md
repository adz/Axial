---
title: "Data.optionalAssoc"
linkTitle: "optionalAssoc"
weight: 2202
---

Associates an object field name with <code>Some</code> value, or omits <code>None</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Data.optionalAssoc&#32;<span>name&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code><span>^value&#32;option</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-datafield.md">DataField</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">optionalAssoc</span> <span class="s">&quot;nickname&quot;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">None</span> <span class="c">// an omitted DataField</span>
</code></pre>

<div popover class="fsdocs-tip" id="fs2">union case Option.None: Option&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L14-14)
