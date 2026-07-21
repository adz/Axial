---
title: "Flow.FlowStream.collect"
linkTitle: "collect"
weight: 2301
---

Maps each value to a stream and concatenates the resulting streams.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.collect&#32;<span>mapper&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'a&#32;->&#32;<span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'d</span>&gt;</span></span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'d</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">collect</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">fromSeq</span>
</code></pre>
