---
title: "Data.applyEdit"
linkTitle: "applyEdit"
weight: 2311
---

Applies one prepared edit or raises <code>DataPatchException</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Data.applyEdit&#32;<span>edit&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `edit` | <code><a href="t-dataedit.md">DataEdit</a></code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">applyEdit</span> <span class="pn">(</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">set</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Grace&quot;</span><span class="pn">)</span>
 <span class="c">// data [ &quot;name&quot; =&gt; &quot;Grace&quot; ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>
<div popover class="fsdocs-tip" id="fs2">val set: elements: &#39;T seq -&gt; Set&lt;&#39;T&gt; (requires comparison)</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L424-424)
