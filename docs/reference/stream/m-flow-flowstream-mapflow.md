---
title: "Flow.FlowStream.mapFlow"
linkTitle: "mapFlow"
weight: 2204
---

Transforms every value with a Flow effect.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.mapFlow&#32;<span>mapper&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'a&#32;->&#32;<span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'b,&#32;'c,&#32;'d</span>&gt;</span></span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'b,&#32;'c,&#32;'d</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">ids</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">mapFlow</span> <span class="id">load</span>
</code></pre>
