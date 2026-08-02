---
title: "Data.put"
linkTitle: "put"
weight: 2314
---

Replaces one value, or adds a missing final object field, and returns the changed tree.

## Signature

<div class="fsdocs-usage">
<code><span>Data.put&#32;<span>path&#32;value&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `value` | <code>^a</code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">put</span> <span class="s">&quot;active&quot;</span> <span class="k">true</span>
 <span class="c">// data [ &quot;name&quot; =&gt; &quot;Ada&quot;; &quot;active&quot; =&gt; true ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L439-439)
