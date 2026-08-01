---
title: "Data.tryMatch"
linkTitle: "tryMatch"
weight: 2508
---

Checks path-based expectations and accumulates structured mismatches.

## Signature

<div class="fsdocs-usage">
<code><span>Data.tryMatch&#32;<span>expectations&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expectations` | <code><span><a href="t-dataexpectation.md">DataExpectation</a>&#32;list</span></code> |  |
| `actual` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="t-datamismatch.md">DataMismatch</a>&#32;list</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">tryMatch</span> <span class="pn">[</span> <span class="id">at</span> <span class="s">&quot;name&quot;</span> <span class="s">&quot;Ada&quot;</span> <span class="pn">]</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;name&quot;</span> <span class="o">=&gt;</span> <span class="s">&quot;Grace&quot;</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// Error [ mismatch at path &quot;name&quot;: expected &quot;Ada&quot;, found &quot;Grace&quot; ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L505-505)
