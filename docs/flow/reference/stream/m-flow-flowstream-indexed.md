---
title: "Flow.FlowStream.indexed"
linkTitle: "indexed"
weight: 2210
---

Emits each value paired with its zero-based index.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.indexed&#32;<span>arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'c</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;<span>(<span>int&#32;*&#32;'c</span>)</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">indexed</span>
</code></pre>
