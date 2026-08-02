---
title: "Data.tryObject"
linkTitle: "tryObject"
weight: 2706
---

Attempts to extract ordered fields from one object value.

## Signature

<div class="fsdocs-usage">
<code><span>Data.tryObject&#32;<span>_arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `_arg1` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><span><span>(<span>string&#32;*&#32;<a href="t-data.md">Data</a></span>)</span>&#32;list</span>&#32;option</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">tryObject</span> <span class="pn">(</span><span class="id">Data</span><span class="pn">.</span><span class="id">Object</span> <span class="pn">[</span><span class="pn">]</span><span class="pn">)</span> <span class="c">// Some []</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L241-241)
