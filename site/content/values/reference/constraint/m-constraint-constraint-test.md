---
title: "Constraint.test"
linkTitle: "test"
weight: 2200
type: docs
---

Answers whether a value satisfies a constraint, without building a violation.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.test&#32;<span>constraint'&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>bool</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">retryCount</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">between</span> <span class="n">0</span> <span class="n">10</span>
 <span class="n">3</span> <span class="o">|&gt;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">test</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">retryCount</span> <span class="c">// true</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val retryCount: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L90-90)
