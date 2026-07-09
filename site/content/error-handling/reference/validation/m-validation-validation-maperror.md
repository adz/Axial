---
title: "Validation.Validation.mapError"
linkTitle: "mapError"
weight: 2108
type: docs
---

Maps the error type of a validation graph.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.mapError&#32;<span>mapper&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error&#32;->&#32;'nextError</span></code> | A function of type <code>&#39;error -&gt; &#39;nextError</code>. |
| `validation` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source <a href="https://learn.microsoft.com/dotnet/api/axial.validation-2">Validation</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'nextError</span>&gt;</span></code> | A validation with transformed error values. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">validation</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">mapError</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">e</span> <span class="k">-&gt;</span> <span class="id">e</span><span class="pn">.</span><span class="id">ToString</span><span class="pn">(</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
