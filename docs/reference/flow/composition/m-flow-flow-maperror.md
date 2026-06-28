---
title: "Flow.Flow.mapError"
linkTitle: "mapError"
weight: 2322
---

Maps the error value of a synchronous flow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.mapError&#32;<span>mapper&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'error&#32;->&#32;'nextError</span></code> | The function to transform the error value. |
| `flow` | <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'nextError,&#32;'value</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.flow-3">Flow</a> with the transformed error type. |

## Remarks


 Transforms the error type of the flow while leaving successful values untouched.
 Useful for mapping internal errors into public-facing domain errors.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fail</span> <span class="s">&quot;error&quot;</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">mapError</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">err</span> <span class="k">-&gt;</span> <span class="id">err</span> <span class="o">+</span> <span class="s">&quot;!&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
