---
title: "Data.diff"
linkTitle: "diff"
weight: 2506
---

Returns all exact structural differences between two values.

## Signature

<div class="fsdocs-usage">
<code><span>Data.diff&#32;<span>expected&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code><a href="t-data.md">Data</a></code> |  |
| `actual` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-datadifference.md">DataDifference</a>&#32;list</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">diff</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span><span class="pn">)</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Grace&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// one DifferentValue difference at path &quot;name&quot;</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L197-197)
