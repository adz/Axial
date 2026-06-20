---
title: "Validation.Validation.apply"
linkTitle: "apply"
weight: 2111
type: docs
---

Applies a validation-wrapped function to a validation-wrapped value.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.apply&#32;<span>validation&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `validation` | <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span><span>(<span>'value&#32;->&#32;'next</span>)</span>,&#32;'error</span>&gt;</span></code> | The validation containing the function. |
| `value` | <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation containing the value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-validation-validation-2.html">Validation</a>&lt;<span>'next,&#32;'error</span>&gt;</span></code> | The result of applying the function to the value, with accumulated errors. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">fn</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">v</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span>
 <span class="id">Validation</span><span class="pn">.</span><span class="id">apply</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">fn</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="4" class="id">v</span> <span class="c">// Validation (Ok 6)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val fn: obj</div>
<div popover class="fsdocs-tip" id="fs2">val v: obj</div>
