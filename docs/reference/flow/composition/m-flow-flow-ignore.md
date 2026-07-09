---
title: "Flow.ignore"
linkTitle: "ignore"
weight: 2330
---

Maps the successful value of a synchronous flow to <code>unit</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.ignore&#32;<span>flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that succeeds with <code>unit</code> instead of the original value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">42</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">ignore</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
