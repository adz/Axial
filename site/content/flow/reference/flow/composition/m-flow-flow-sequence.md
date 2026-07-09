---
title: "Flow.sequence"
linkTitle: "sequence"
weight: 2335
type: docs
---

Transforms a sequence of flows into a flow of a sequence and stops at the first failure.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.sequence&#32;<span>flows</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flows` | <code><span><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span>&#32;seq</span></code> | The sequence of flows to run. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>'value&#32;list</span></span>&gt;</span></code> | A flow containing a list of the successful values. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">sequence</span> <span class="pn">[</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span><span class="pn">;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">2</span><span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
