---
title: "Refined.Refinement.constraint"
linkTitle: "constraint"
weight: 2604
---

Returns the constraint the refinement admits by.

## Signature

<div class="fsdocs-usage">
<code><span>Refinement.constraint'&#32;<span>refinement</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `refinement` | <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'underlying,&#32;'refined</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../constraint/t-constraint-constraint.md">Constraint</a>&lt;'underlying&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">retryCount</span> <span class="o">|&gt;</span> <span class="id">Refinement</span><span class="pn">.</span><span class="id">constraint&#39;</span> <span class="o">|&gt;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">inspect</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refinement.fs#L65-65)
