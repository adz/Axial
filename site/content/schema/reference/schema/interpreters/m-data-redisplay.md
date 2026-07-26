---
title: "Data.redisplay"
linkTitle: "redisplay"
weight: 2009
type: docs
---


 Redisplays a scalar structured data value, returning blank text for missing, object-shaped, or collection-shaped input.


## Signature

<div class="fsdocs-usage">
<code><span>Data.redisplay&#32;<span>input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `input` | <code><a href="../../data/t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;42&quot;</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">Data</span><span class="pn">.</span><span class="id">redisplay</span>
 <span class="c">// &quot;42&quot;</span>

 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">Data</span><span class="pn">.</span><span class="id">Null</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">Data</span><span class="pn">.</span><span class="id">redisplay</span>
 <span class="c">// &quot;&quot;</span>

 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="5" class="id">Data</span><span class="pn">.</span><span class="id">objectOfList</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="6" class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="7" class="id">Data</span><span class="pn">.</span><span class="id">redisplay</span>
 <span class="c">// &quot;&quot; (object-shaped input has no scalar to redisplay)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataOperations.fs#L611-611)
