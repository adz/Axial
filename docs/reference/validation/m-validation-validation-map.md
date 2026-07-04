---
title: "Validation.Validation.map"
linkTitle: "map"
weight: 2106
---

Maps the successful value of a validation.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.map&#32;<span>mapper&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'value&#32;->&#32;'next</span></code> | A function of type <code>&#39;value -&gt; &#39;next</code>. |
| `validation` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source <a href="https://learn.microsoft.com/dotnet/api/axial.validation-2">Validation</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'next,&#32;'error</span>&gt;</span></code> | A validation with the transformed success value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">map</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="pn">*</span> <span class="n">2</span><span class="pn">)</span> <span class="c">// Validation (Ok 10)</span>
</code></pre>
