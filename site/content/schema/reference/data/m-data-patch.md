---
title: "Data.patch"
linkTitle: "patch"
weight: 2312
type: docs
---

Applies edits atomically or raises <code>DataPatchException</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Data.patch&#32;<span>edits&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `edits` | <code><span><a href="t-dataedit.md">DataEdit</a>&#32;list</span></code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">data</span> <span class="pn">[</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">Data</span><span class="pn">.</span><span class="id">assoc</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span>
 <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">Data</span><span class="pn">.</span><span class="id">patch</span> <span class="pn">[</span> <span class="id">DataEdit</span><span class="pn">.</span><span class="id">set</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Grace&quot;</span> <span class="pn">]</span>
 <span class="c">// Data.data [ Data.assoc &quot;name&quot; &quot;Grace&quot; ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L416-416)
