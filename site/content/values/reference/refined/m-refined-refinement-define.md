---
title: "Refined.Refinement.define"
linkTitle: "define"
weight: 2601
type: docs
---

Defines a refinement from one constraint, a constructor, and the reverse projection.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.define&#32;<span>constraint'&#32;construct&#32;project</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="../constraint/t-constraint-constraint.md">Constraint</a>&lt;'underlying&gt;</span></code> |  |
| `construct` | <code><span>'underlying&#32;->&#32;'refined</span></code> |  |
| `project` | <code><span>'refined&#32;->&#32;'underlying</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'underlying,&#32;'refined</span>&gt;</span></code> |  |

## Remarks


 Compose several rules with <code>Constraint.all</code> before defining, and reach for <code>Constraint.custom</code>
 when the rule is an arbitrary predicate. Both produce an ordinary constraint, so there is no separate
 plural or check-taking constructor.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">retryCount</span> <span class="o">=</span>
     <span class="id">Refinement</span><span class="pn">.</span><span class="id">define</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">between</span> <span class="n">0</span> <span class="n">10</span><span class="pn">)</span> <span class="id">RetryCount</span> <span class="id">_</span><span class="pn">.</span><span class="id">Value</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val retryCount: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refinement.fs#L39-39)
