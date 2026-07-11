---
title: "Flow.FlowStream.unfoldFlow"
linkTitle: "unfoldFlow"
weight: 2104
---

Creates a cold stream by repeatedly running an effectful state transition.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.unfoldFlow&#32;<span>step&#32;initialState</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `step` | <code><span>'state&#32;->&#32;<span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span><span>(<span>'value&#32;*&#32;'state</span>)</span>&#32;option</span></span>&gt;</span></span></code> | Returns <code>Some(value, nextState)</code> or <code>None</code> when the stream is complete. |
| `initialState` | <code>'state</code> | The state used for the first pull. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">FlowStream</span><span class="pn">.</span><span class="id">unfoldFlow</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">n</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">ok</span> <span class="pn">(</span><span class="k">if</span> <span class="id">n</span> <span class="pn">&lt;</span> <span class="n">3</span> <span class="k">then</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Some</span><span class="pn">(</span><span class="id">n</span><span class="pn">,</span> <span class="id">n</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span> <span class="k">else</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">None</span><span class="pn">)</span><span class="pn">)</span> <span class="n">0</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Option.None: Option&lt;&#39;T&gt;</div>
