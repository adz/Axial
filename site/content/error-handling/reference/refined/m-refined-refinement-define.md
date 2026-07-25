---
title: "Refined.Refinement.define"
linkTitle: "define"
weight: 2801
---

Defines a refinement from its smart constructor and raw-value projection.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.define&#32;<span>create&#32;inspect</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `create` | <code><span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="types/t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></span></code> |  |
| `inspect` | <code><span>'value&#32;->&#32;'raw</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'raw,&#32;'value</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">email</span> <span class="o">=</span>
     <span class="id">Refinement</span><span class="pn">.</span><span class="id">define</span> <span class="id">Email</span><span class="pn">.</span><span class="id">create</span> <span class="id">Email</span><span class="pn">.</span><span class="id">value</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val email: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L606-606)
