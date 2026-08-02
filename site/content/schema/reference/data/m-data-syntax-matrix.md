---
title: "Data.Syntax.matrix"
linkTitle: "matrix"
weight: 2406
type: docs
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

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">matrix</span> <span class="pn">[</span> <span class="id">dimension</span> <span class="s">&quot;status&quot;</span> <span class="pn">[</span> <span class="id">variant</span> <span class="s">&quot;active&quot;</span> <span class="pn">[</span><span class="pn">]</span><span class="pn">;</span> <span class="id">variant</span> <span class="s">&quot;inactive&quot;</span> <span class="pn">[</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">set</span> <span class="s">&quot;active&quot;</span> <span class="k">false</span> <span class="pn">]</span> <span class="pn">]</span> <span class="pn">]</span> <span class="id">baseline</span>
 <span class="c">// cases named &quot;status: active&quot; and &quot;status: inactive&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val set: elements: &#39;T seq -&gt; Set&lt;&#39;T&gt; (requires comparison)</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L749-749)
