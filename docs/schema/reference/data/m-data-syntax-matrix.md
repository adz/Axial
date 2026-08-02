---
title: "Data.Syntax.matrix"
linkTitle: "matrix"
weight: 2406
---

Materializes a deterministic Cartesian matrix, limited to 256 cases.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.matrix&#32;<span>dimensions&#32;baseline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `dimensions` | <code><span><a href="t-datadimension.md">DataDimension</a>&#32;list</span></code> |  |
| `baseline` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-datacase.md">DataCase</a>&#32;list</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">matrix</span> <span class="pn">[</span> <span class="id">dimension</span> <span class="s">&quot;status&quot;</span> <span class="pn">[</span> <span class="id">variant</span> <span class="s">&quot;active&quot;</span> <span class="pn">[</span><span class="pn">]</span><span class="pn">;</span> <span class="id">variant</span> <span class="s">&quot;inactive&quot;</span> <span class="pn">[</span> <span class="id">replace</span> <span class="s">&quot;active&quot;</span> <span class="k">false</span> <span class="pn">]</span> <span class="pn">]</span> <span class="pn">]</span> <span class="id">baseline</span>
 <span class="c">// cases named &quot;status: active&quot; and &quot;status: inactive&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L837-837)
