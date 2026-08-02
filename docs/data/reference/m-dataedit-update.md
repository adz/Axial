---
title: "DataEdit.update"
linkTitle: "update"
weight: 2310
---

Describes applying a function to an existing value.

## Signature

<div class="fsdocs-usage">
<code><span>DataEdit.update&#32;<span>path&#32;change</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `change` | <code><span><a href="t-data.md">Data</a>&#32;->&#32;<a href="t-data.md">Data</a></span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-dataedit.md">DataEdit</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">DataEdit</span><span class="pn">.</span><span class="id">update</span> <span class="s">&quot;active&quot;</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">_</span> <span class="k">-&gt;</span> <span class="id">Data</span><span class="pn">.</span><span class="id">Bool</span> <span class="k">false</span><span class="pn">)</span> <span class="c">// one DataEdit</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L115-115)
