---
title: "Data.insert"
linkTitle: "insert"
weight: 2318
---

Inserts one item at a valid list index and returns the changed tree.

## Signature

<div class="fsdocs-usage">
<code><span>Data.insert&#32;<span>path&#32;index&#32;value&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `index` | <code>int</code> |  |
| `value` | <code>^a</code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;roles&quot;</span> <span class="o">=&gt;</span> <span class="pn">[</span> <span class="s">&quot;author&quot;</span> <span class="pn">]</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">insert</span> <span class="s">&quot;roles&quot;</span> <span class="n">1</span> <span class="s">&quot;admin&quot;</span>
 <span class="c">// data [ &quot;roles&quot; =&gt; [ &quot;author&quot;; &quot;admin&quot; ] ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L564-564)
