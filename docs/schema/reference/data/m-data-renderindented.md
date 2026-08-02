---
title: "Data.renderIndented"
linkTitle: "renderIndented"
weight: 2701
---

Renders structured data as deterministic indented JSON.

## Signature

<div class="fsdocs-usage">
<code><span>Data.renderIndented&#32;<span>input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">renderIndented</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// {</span>
 <span class="c">//   &quot;name&quot;: &quot;Ada&quot;</span>
 <span class="c">// }</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L648-648)
