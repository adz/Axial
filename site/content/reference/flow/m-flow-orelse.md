---
title: "Flow.orElse"
linkTitle: "orElse"
weight: 2318
type: docs
---

Falls back to another flow when the source flow fails.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.orElse&#32;<span>fallback&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The flow to run if the source flow fails. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that recovers from errors using the fallback flow. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fail</span> <span class="s">&quot;error&quot;</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">orElse</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="s">&quot;recovered&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>



