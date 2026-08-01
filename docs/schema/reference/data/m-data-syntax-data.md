---
title: "Data.Syntax.data"
linkTitle: "data"
weight: 2201
---

Builds an object from ordered field instructions.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.data&#32;<span>fields</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fields` | <code><span><a href="t-datafield.md">DataField</a>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span><span class="pn">;</span> <span class="s">&quot;active&quot;</span> <span class="o">=&gt;</span> <span class="k">true</span> <span class="pn">]</span>
 <span class="c">// Data.Object [ &quot;name&quot;, Data.Text &quot;Ada&quot;; &quot;active&quot;, Data.Bool true ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L591-591)
