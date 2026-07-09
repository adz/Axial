---
title: "Validation.map3"
linkTitle: "map3"
weight: 2110
type: docs
---

Combines three validations, accumulating errors when any input fails.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.map3&#32;<span>mapper&#32;left&#32;middle&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'left&#32;->&#32;'middle&#32;->&#32;'right&#32;->&#32;'value</span></code> | A function of type <code>&#39;left -&gt; &#39;middle -&gt; &#39;right -&gt; &#39;value</code>. |
| `left` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'left,&#32;'error</span>&gt;</span></code> | The first validation. |
| `middle` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'middle,&#32;'error</span>&gt;</span></code> | The second validation. |
| `right` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'right,&#32;'error</span>&gt;</span></code> | The third validation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A validation with the combined result. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v1</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">1</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">v2</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">2</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">v3</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">3</span>
 <span class="id">Validation</span><span class="pn">.</span><span class="id">map3</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="id">y</span> <span class="id">z</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="id">y</span> <span class="o">+</span> <span class="id">z</span><span class="pn">)</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">v1</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="id">v2</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="6" class="id">v3</span> <span class="c">// Validation (Ok 6)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v1: obj</div>
<div popover class="fsdocs-tip" id="fs2">val v2: obj</div>
<div popover class="fsdocs-tip" id="fs3">val v3: obj</div>
