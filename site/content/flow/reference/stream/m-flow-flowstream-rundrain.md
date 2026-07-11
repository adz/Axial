---
title: "Flow.FlowStream.runDrain"
linkTitle: "runDrain"
weight: 2404
type: docs
---

Consumes a stream and ignores its values.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.runDrain&#32;<span>stream</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `stream` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'a,&#32;'b,&#32;'c</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;'b,&#32;unit</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">runDrain</span>
</code></pre>
