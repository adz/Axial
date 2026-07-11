---
title: "Flow.FlowStream.zip"
linkTitle: "zip"
weight: 2302
type: docs
---

Pairs values from two streams until either stream ends.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.zip&#32;<span>arg1&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'c</span>&gt;</span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'d</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;<span>(<span>'d&#32;*&#32;'c</span>)</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">left</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">zip</span> <span class="id">right</span>
</code></pre>
