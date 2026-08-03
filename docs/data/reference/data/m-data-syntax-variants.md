---
title: "Data.Syntax.variants"
linkTitle: "variants"
weight: 2404
---

Materializes named variations from one baseline.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.variants&#32;<span>variations&#32;baseline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `variations` | <code><span><a href="t-datavariation.md">DataVariation</a>&#32;list</span></code> |  |
| `baseline` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-datacase.md">DataCase</a>&#32;list</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">variants</span> <span class="pn">[</span> <span class="id">variant</span> <span class="s">&quot;inactive&quot;</span> <span class="pn">[</span> <span class="id">replace</span> <span class="s">&quot;active&quot;</span> <span class="k">false</span> <span class="pn">]</span> <span class="pn">]</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;active&quot;</span> <span class="o">=&gt;</span> <span class="k">true</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// [ { Name = &quot;inactive&quot;; Value = data [ &quot;active&quot; =&gt; false ] } ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L301-301)
