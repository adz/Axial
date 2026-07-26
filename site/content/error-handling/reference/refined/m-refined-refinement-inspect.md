---
title: "Refined.Refinement.inspect"
linkTitle: "inspect"
weight: 2803
type: docs
---

Returns the raw representation stored by a refined value.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.inspect&#32;<span>refinement&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `refinement` | <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'raw,&#32;'value</span>&gt;</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>'raw</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">raw</span> <span class="o">=</span> <span class="id">Refinement</span><span class="pn">.</span><span class="id">inspect</span> <span class="id">Email</span><span class="pn">.</span><span class="id">refinement</span> <span class="id">email</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val raw: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L630-630)
