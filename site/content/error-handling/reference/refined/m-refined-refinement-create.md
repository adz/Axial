---
title: "Refined.Refinement.create"
linkTitle: "create"
weight: 2802
---

Runs the refinement&#39;s smart constructor.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.create&#32;<span>refinement&#32;raw</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `refinement` | <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'raw,&#32;'value</span>&gt;</span></code> |  |
| `raw` | <code>'raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="types/t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span class="id">Refinement</span><span class="pn">.</span><span class="id">create</span> <span class="id">Email</span><span class="pn">.</span><span class="id">refinement</span> <span class="id">raw</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L620-620)
