---
title: "Constraint.inspect"
linkTitle: "inspect"
weight: 2203
---

Returns the constraint&#39;s inspectable description.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.inspect&#32;<span>constraint'</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-constraintdescription.md">ConstraintDescription</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">inspect</span> <span class="id">requiredName</span><span class="pn">)</span><span class="pn">.</span><span class="id">Expression</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L108-108)
