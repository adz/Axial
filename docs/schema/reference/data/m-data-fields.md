---
title: "Data.fields"
linkTitle: "fields"
weight: 2205
---

Returns exact field instructions from an existing object.

## Signature

<div class="fsdocs-usage">
<code><span>Data.fields&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-datafield.md">DataField</a>&#32;list</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">fields</span> <span class="pn">(</span><span class="id">Data</span><span class="pn">.</span><span class="id">data</span> <span class="pn">[</span> <span class="id">Data</span><span class="pn">.</span><span class="id">assoc</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// one field instruction for name</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L30-30)
