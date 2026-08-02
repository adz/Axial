---
title: "Data.Json.render"
linkTitle: "render"
weight: 2601
type: docs
---

Renders compact deterministic JSON.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Json.render&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-data.md">Data</a>&#32;->&#32;string</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">Json</span><span class="pn">.</span><span class="id">render</span> <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">Data</span><span class="pn">.</span><span class="id">Object</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// {&quot;name&quot;:&quot;Ada&quot;}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L841-841)
