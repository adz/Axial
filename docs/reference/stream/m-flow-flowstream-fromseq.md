---
title: "Flow.FlowStream.fromSeq"
linkTitle: "fromSeq"
weight: 2102
---

Creates a stream from a synchronous sequence of values.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.fromSeq&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence of values to be emitted by the stream. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.flowstream-3">FlowStream</a> that yields each value from the sequence. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">stream</span> <span class="o">=</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">fromSeq</span> <span class="pn">[</span><span class="n">1..</span><span class="n">10</span><span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val stream: obj</div>
