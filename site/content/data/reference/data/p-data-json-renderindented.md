---
title: "Data.Json.renderIndented"
linkTitle: "renderIndented"
weight: 2601
type: docs
---

Renders indented deterministic JSON.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Json.renderIndented&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-data.md">Data</a>&#32;->&#32;string</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">Json</span><span class="pn">.</span><span class="id">renderIndented</span> <span class="pn">(</span><span class="id">Data</span><span class="pn">.</span><span class="id">Object</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span><span class="pn">,</span> <span class="id">Data</span><span class="pn">.</span><span class="id">Text</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// {</span>
 <span class="c">//   &quot;name&quot;: &quot;Ada&quot;</span>
 <span class="c">// }</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L412-412)
