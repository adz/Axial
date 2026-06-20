---
title: "Flow.FlowStream.map"
linkTitle: "map"
weight: 2101
---

Transforms the successful values of a stream using the provided function.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.map&#32;<span>f&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `f` | <code><span>'v&#32;->&#32;'w</span></code> | The function to transform each value. |
| `arg1` | <code><span><a href="/reference/Axial/axial-flow-flowstream-3.html">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'v</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flowstream-3.html">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'w</span>&gt;</span></code> | A new stream that yields transformed values. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">stream</span> <span class="o">=</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">fromSeq</span> <span class="pn">[</span><span class="n">1</span><span class="pn">;</span> <span class="n">2</span><span class="pn">;</span> <span class="n">3</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">map</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">n</span> <span class="k">-&gt;</span> <span class="id">n</span> <span class="pn">*</span> <span class="n">2</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val stream: obj</div>
