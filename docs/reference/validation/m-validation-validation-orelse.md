---
title: "Validation.Validation.orElse"
linkTitle: "orElse"
weight: 2113
---

Falls back to another validation when the source validation fails.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.orElse&#32;<span>fallback&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation to use when the source fails. |
| `validation` | <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source validation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source validation when it succeeds, otherwise the fallback validation. |

## Remarks


 This is a left-biased choice operator. If the source succeeds, the fallback is not used.
 If the source fails, the fallback validation is returned as-is.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v1</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">fail</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;err&quot;</span><span class="pn">)</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">v2</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span>
 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">v1</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">orElse</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="4" class="id">v2</span> <span class="c">// Validation (Ok 5)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v1: obj</div>
<div popover class="fsdocs-tip" id="fs2">val v2: obj</div>
