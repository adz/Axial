---
title: "Flow.FlowStream.scan"
linkTitle: "scan"
weight: 2211
---

Emits successive accumulator states.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.scan&#32;<span>folder&#32;initial&#32;arg3</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `folder` | <code><span>'a&#32;->&#32;'b&#32;->&#32;'a</span></code> |  |
| `initial` | <code>'a</code> |  |
| `arg2` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'c,&#32;'d,&#32;'b</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'c,&#32;'d,&#32;'a</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">scan</span> <span class="pn">(</span><span class="o">+</span><span class="pn">)</span> <span class="n">0</span>
</code></pre>
