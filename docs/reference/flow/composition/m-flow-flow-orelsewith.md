---
title: "Flow.orElseWith"
linkTitle: "orElseWith"
weight: 2324
---

Computes a fallback flow from the typed error when the source flow fails.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.orElseWith&#32;<span>fallback&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code><span>'error&#32;->&#32;<span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></span></code> | A function that produces a new flow from the error value. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that recovers from errors using the fallback function. |

## Remarks


 The fallback runs only for expected typed failures represented by <code>Cause.Fail</code>. It does
 not catch interruption or defects. Use this for domain-level recovery, not for swallowing
 cancellation or unexpected exceptions.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fail</span> <span class="s">&quot;error&quot;</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">orElseWith</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">err</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="s">&quot;recovered&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
