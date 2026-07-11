---
title: "Flow.FlowStream.mapError"
linkTitle: "mapError"
weight: 2201
type: docs
---

Transforms the typed error channel of a stream.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.mapError&#32;<span>mapper&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error&#32;->&#32;'nextError</span></code> |  |
| `arg1` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'nextError,&#32;'value</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">mapError</span> <span class="id">DomainError</span>
</code></pre>
