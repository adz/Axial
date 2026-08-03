---
title: "Refined.Refinement.underlying"
linkTitle: "underlying"
weight: 2603
type: docs
---

Returns the canonical underlying representation of a refined value.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.underlying&#32;<span>refinement&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `refinement` | <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'underlying,&#32;'refined</span>&gt;</span></code> |  |
| `value` | <code>'refined</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>'underlying</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">RetryCount</span> <span class="n">3</span> <span class="o">|&gt;</span> <span class="id">Refinement</span><span class="pn">.</span><span class="id">underlying</span> <span class="id">retryCount</span> <span class="c">// 3</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refinement.fs#L59-59)
