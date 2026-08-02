---
title: "Data.rename"
linkTitle: "rename"
weight: 2319
---

Renames one existing object field without moving it and returns the changed tree.

## Signature

<div class="fsdocs-usage">
<code><span>Data.rename&#32;<span>path&#32;name&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `name` | <code>string</code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">rename</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;displayName&quot;</span>
 <span class="c">// data [ &quot;displayName&quot; =&gt; &quot;Ada&quot; ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L472-472)
