---
title: "Data.redisplayPath"
linkTitle: "redisplayPath"
weight: 2010
type: docs
---

Parses an input path and redisplays the addressed scalar structured data value.

## Signature

<div class="fsdocs-usage">
<code><span>Data.redisplayPath&#32;<span>path&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `input` | <code><a href="../../data/t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">input</span> <span class="o">=</span>
     <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">Data</span><span class="pn">.</span><span class="id">objectOfList</span> <span class="pn">[</span>
         <span class="s">&quot;address&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="3" class="id">Data</span><span class="pn">.</span><span class="id">objectOfList</span> <span class="pn">[</span> <span class="s">&quot;city&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="4" class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;Boston&quot;</span> <span class="pn">]</span>
         <span class="s">&quot;tags&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="id">Data</span><span class="pn">.</span><span class="id">List</span> <span class="pn">[</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="6" class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;admin&quot;</span><span class="pn">;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="7" class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;billing&quot;</span> <span class="pn">]</span>
     <span class="pn">]</span>

 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="8" class="id">Data</span><span class="pn">.</span><span class="id">redisplayPath</span> <span class="s">&quot;address.city&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="9" class="id">input</span>
 <span class="c">// &quot;Boston&quot;</span>

 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="10" class="id">Data</span><span class="pn">.</span><span class="id">redisplayPath</span> <span class="s">&quot;tags[1]&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="11" class="id">input</span>
 <span class="c">// &quot;billing&quot;</span>

 <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="12" class="id">Data</span><span class="pn">.</span><span class="id">redisplayPath</span> <span class="s">&quot;address.zip&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="13" class="id">input</span>
 <span class="c">// &quot;&quot; (path not present)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val input: obj</div>
<div popover class="fsdocs-tip" id="fs2">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataOperations.fs#L648-648)
