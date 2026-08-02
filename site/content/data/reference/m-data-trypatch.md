---
title: "Data.tryPatch"
linkTitle: "tryPatch"
weight: 2329
type: docs
---

Applies immutable edits atomically in declaration order.

## Signature

<div class="fsdocs-usage">
<code><span>Data.tryPatch&#32;<span>edits&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `edits` | <code><span><a href="t-dataedit.md">DataEdit</a>&#32;list</span></code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><a href="t-data.md">Data</a>,&#32;<span><a href="t-datapatchfailure.md">DataPatchFailure</a>&#32;list</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">tryPatch</span> <span class="pn">[</span> <span class="id">replace</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Grace&quot;</span> <span class="pn">]</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// Ok (data [ &quot;name&quot; =&gt; &quot;Grace&quot; ])</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L122-122)
