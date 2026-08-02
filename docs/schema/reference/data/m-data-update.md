---
title: "Data.update"
linkTitle: "update"
weight: 2320
---

Applies one function to an existing value and returns the changed tree.

## Signature

<div class="fsdocs-usage">
<code><span>Data.update&#32;<span>path&#32;change&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `change` | <code><span><a href="t-data.md">Data</a>&#32;->&#32;<a href="t-data.md">Data</a></span></code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;active&quot;</span> <span class="o">=&gt;</span> <span class="k">true</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Data</span><span class="pn">.</span><span class="id">update</span> <span class="s">&quot;active&quot;</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">_</span> <span class="k">-&gt;</span> <span class="id">Data</span><span class="pn">.</span><span class="id">Bool</span> <span class="k">false</span><span class="pn">)</span>
 <span class="c">// data [ &quot;active&quot; =&gt; false ]</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L191-191)
