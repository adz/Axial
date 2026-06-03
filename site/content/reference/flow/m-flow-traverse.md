---
title: "Flow.traverse"
linkTitle: "traverse"
weight: 2327
type: docs
---

Transforms a sequence of values into a flow and stops at the first failure.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.traverse&#32;<span>mapping&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapping` | <code><span>'value&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'next</span>&gt;</span></span></code> | A function that maps each value to a flow. |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence of values to transform. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>'next&#32;list</span></span>&gt;</span></code> | A flow containing a list of the successful mapped values. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flows</span> <span class="o">=</span> <span class="pn">[</span><span class="n">1</span><span class="pn">;</span> <span class="n">2</span><span class="pn">;</span> <span class="n">3</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">traverse</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="pn">(</span><span class="id">x</span> <span class="pn">*</span> <span class="n">2</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flows: obj</div>
