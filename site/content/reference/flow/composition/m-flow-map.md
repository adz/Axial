---
title: "Flow.map"
linkTitle: "map"
weight: 2314
type: docs
---

Transforms the successful value of a flow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.map&#32;<span>mapper&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'value&#32;->&#32;'next</span></code> | A function of type <code>&#39;value -&gt; &#39;next</code> to transform the successful value. |
| `flow` | <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow of type <a href="../t-flow.md">Flow</a> to transform. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'next</span>&gt;</span></code> | A new <a href="../t-flow.md">Flow</a> with the transformed success value of type <code>&#39;next</code>. |

## Remarks


 If the source <span class="fsdocs-param-name">flow</span> fails, the <span class="fsdocs-param-name">mapper</span> is not executed.
 The original failure cause is preserved, including typed failures, interruption, and defects.
 Use <code>map</code> for pure value transformations after an effect has succeeded.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">map</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
