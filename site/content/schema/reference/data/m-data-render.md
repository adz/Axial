---
title: "Data.render"
linkTitle: "render"
weight: 2700
type: docs
---

Renders structured data in a compact, human-readable form.

## Signature

<div class="fsdocs-usage">
<code><span>Data.render&#32;<span>input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">render</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Ada&quot;</span><span class="pn">;</span> <span class="s">&quot;active&quot;</span> <span class="o">=&gt;</span> <span class="k">true</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// { name: &quot;Ada&quot;, active: true }</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L214-214)
