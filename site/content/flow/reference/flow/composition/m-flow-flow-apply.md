---
title: "Flow.apply"
linkTitle: "apply"
weight: 2329
type: docs
---

Applies a flow-wrapped function to a flow-wrapped value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.apply&#32;<span>flow&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>(<span>'value&#32;->&#32;'next</span>)</span></span>&gt;</span></code> | A flow that contains a function to apply. |
| `value` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that contains the value to apply the function to. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'next</span>&gt;</span></code> | A flow containing the result of applying the function to the value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">apply</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
