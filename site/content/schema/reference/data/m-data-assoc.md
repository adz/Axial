---
title: "Data.assoc"
linkTitle: "assoc"
weight: 2201
type: docs
---

Associates an object field name with one exact value.

## Signature

<div class="fsdocs-usage">
<code><span>Data.assoc&#32;<span>name&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>^a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-datafield.md">DataField</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">assoc</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Ada&quot;</span> <span class="c">// one DataField</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L195-195)
