---
title: "Flow.FlowStream.tapFlow"
linkTitle: "tapFlow"
weight: 2205
type: docs
---

Runs an effect for each value before emitting the original value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.tapFlow&#32;<span>action&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `action` | <code><span>'a&#32;->&#32;<span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'b,&#32;'c,&#32;unit</span>&gt;</span></span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">tapFlow</span> <span class="id">logValue</span>
</code></pre>
