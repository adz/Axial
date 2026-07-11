---
title: "Flow.FlowStream.runFold"
linkTitle: "runFold"
weight: 2402
type: docs
---

Folds a stream into one value inside Flow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.runFold&#32;<span>folder&#32;initial&#32;arg3</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `folder` | <code><span>'state&#32;->&#32;'a&#32;->&#32;'state</span></code> |  |
| `initial` | <code>'state</code> |  |
| `arg2` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'a</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'state</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">stream</span> <span class="o">|&gt;</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">runFold</span> <span class="pn">(</span><span class="o">+</span><span class="pn">)</span> <span class="n">0</span>
</code></pre>
