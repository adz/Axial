---
title: "Flow.localEnv"
linkTitle: "localEnv"
weight: 2327
type: docs
---

Runs a flow against an environment derived from the outer environment.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.localEnv&#32;<span>mapping&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapping` | <code><span>'outerEnvironment&#32;->&#32;'innerEnvironment</span></code> | A function that maps the outer environment to the inner environment. |
| `flow` | <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'innerEnvironment,&#32;'error,&#32;'value</span>&gt;</span></code> | The flow to run with the inner environment. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'outerEnvironment,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that expects the outer environment. |

## Remarks


 Use this to embed a smaller workflow inside a larger application environment without changing
 the smaller workflow&#39;s type. The mapping is applied at execution time. This is useful for
 preserving narrow helper signatures while still running everything from one app boundary.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">localEnv</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">outer</span> <span class="k">-&gt;</span> <span class="id">outer</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
