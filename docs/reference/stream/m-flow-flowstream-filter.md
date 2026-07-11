---
title: "Flow.FlowStream.filter"
linkTitle: "filter"
weight: 2202
---

Keeps values that satisfy a predicate.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.filter&#32;<span>predicate&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'a&#32;->&#32;bool</span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">filter</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">value</span> <span class="k">-&gt;</span> <span class="id">value</span> <span class="pn">&gt;</span> <span class="n">0</span><span class="pn">)</span>
</code></pre>
