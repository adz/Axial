---
title: "Flow.FlowStream.runCollect"
linkTitle: "runCollect"
weight: 2403
---

Collects all emitted values into a list.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.runCollect&#32;<span>stream</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `stream` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'c</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'a,&#32;'b,&#32;<span>'c&#32;list</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">runCollect</span>
</code></pre>
