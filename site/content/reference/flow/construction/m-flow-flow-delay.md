---
title: "Flow.Flow.delay"
linkTitle: "delay"
weight: 2333
type: docs
---

Defers flow construction until execution time.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.delay&#32;<span>factory</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `factory` | <code><span>unit&#32;->&#32;<span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></span></code> | A function that returns the flow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that lazily evaluates the factory when executed. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">delay</span> <span class="pn">(</span><span class="k">fun</span> <span class="pn">(</span><span class="pn">)</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">42</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
