---
title: "Flow.Flow.map3"
linkTitle: "map3"
weight: 2328
type: docs
---

Combines three flows with a mapping function.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.map3&#32;<span>mapper&#32;left&#32;middle&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'left&#32;->&#32;'middle&#32;->&#32;'right&#32;->&#32;'value</span></code> | A function that combines the successful values of all three flows. |
| `left` | <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'left</span>&gt;</span></code> | The first flow to run. |
| `middle` | <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'middle</span>&gt;</span></code> | The second flow to run. |
| `right` | <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'right</span>&gt;</span></code> | The third flow to run. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow containing the mapped value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">map3</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="id">y</span> <span class="id">z</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="id">y</span> <span class="o">+</span> <span class="id">z</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">2</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">3</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
