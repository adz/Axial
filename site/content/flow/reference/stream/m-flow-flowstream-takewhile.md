---
title: "Flow.FlowStream.takeWhile"
linkTitle: "takeWhile"
weight: 2208
type: docs
---

Emits values while a predicate remains true.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.takeWhile&#32;<span>predicate&#32;arg2</span></span></code>
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

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">takeWhile</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">value</span> <span class="k">-&gt;</span> <span class="id">value</span> <span class="pn">&lt;</span> <span class="n">100</span><span class="pn">)</span>
</code></pre>
