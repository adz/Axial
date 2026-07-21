---
title: "Flow.FlowStream.singleton"
linkTitle: "singleton"
weight: 2101
---

Creates a stream containing one value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.singleton&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FlowStream</span><span class="pn">.</span><span class="id">singleton</span> <span class="n">42</span>
</code></pre>
