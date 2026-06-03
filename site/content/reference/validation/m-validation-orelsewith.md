---
title: "Validation.orElseWith"
linkTitle: "orElseWith"
weight: 2114
type: docs
---

Computes a fallback validation from the source diagnostics when validation fails.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.orElseWith&#32;<span>fallback&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code><span><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span>&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span></code> | A function that turns the diagnostics into an alternate validation. |
| `validation` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source validation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source validation when it succeeds, otherwise the computed fallback validation. |

## Remarks


 This is the lazy counterpart to <a href="https://learn.microsoft.com/dotnet/api/orelse">orElse</a> and is useful when the alternate
 branch depends on the accumulated diagnostics.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v1</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">fail</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;err&quot;</span><span class="pn">)</span>
 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">v1</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">orElseWith</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">diag</span> <span class="k">-&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">10</span><span class="pn">)</span> <span class="c">// Validation (Ok 10)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v1: obj</div>
