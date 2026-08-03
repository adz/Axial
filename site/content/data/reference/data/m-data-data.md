---
title: "Data.data"
linkTitle: "data"
weight: 2203
type: docs
---

Builds an object from ordered field instructions.

## Signature

<div class="fsdocs-usage">
<code><span>Data.data&#32;<span>fields</span></span></code>
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

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">data</span> <span class="pn">[</span> <span class="id">Data</span><span class="pn">.</span><span class="id">assoc</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span>
 <span class="c">// Data.Object [ &quot;name&quot;, Data.Text &quot;Ada&quot; ]</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L23-23)
