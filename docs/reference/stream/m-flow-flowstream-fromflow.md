---
title: "Flow.FlowStream.fromFlow"
linkTitle: "fromFlow"
weight: 2103
---

Creates a one-element stream from an effectful value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.fromFlow&#32;<span>flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flow` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FlowStream</span><span class="pn">.</span><span class="id">fromFlow</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">ok</span> <span class="n">42</span><span class="pn">)</span>
</code></pre>
