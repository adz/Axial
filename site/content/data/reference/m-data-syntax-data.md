---
title: "Data.Syntax.data"
linkTitle: "data"
weight: 2206
type: docs
---

Builds an object from ordered field instructions.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.data&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><span><a href="t-datafield.md">DataField</a>&#32;list</span>&#32;->&#32;<a href="t-data.md">Data</a></span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span><span class="pn">;</span> <span class="s">&quot;active&quot;</span> <span class="o">=&gt;</span> <span class="k">true</span> <span class="pn">]</span>
 <span class="c">// Data.Object [ &quot;name&quot;, Data.Text &quot;Ada&quot;; &quot;active&quot;, Data.Bool true ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L262-262)
