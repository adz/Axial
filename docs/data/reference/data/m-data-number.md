---
title: "Data.number"
linkTitle: "number"
weight: 2204
---

Constructs a number from one JSON number token, validated identically on .NET and Fable.

## Signature

<div class="fsdocs-usage">
<code><span>Data.number&#32;<span>token</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `token` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">number</span> <span class="s">&quot;1.2300e+4&quot;</span> <span class="c">// Data.Number &quot;1.2300e+4&quot;</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L37-37)
