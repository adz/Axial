---
title: "Flow.FlowStream.take"
linkTitle: "take"
weight: 2206
type: docs
---

Emits at most <span class="fsdocs-param-name">count</span> values.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.take&#32;<span>count&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `count` | <code>int</code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'c</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'c</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">take</span> <span class="n">10</span>
</code></pre>
