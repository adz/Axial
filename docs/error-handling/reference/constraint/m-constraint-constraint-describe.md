---
title: "Constraint.describe"
linkTitle: "describe"
weight: 2307
---

Attaches documentary prose to a constraint.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.describe&#32;<span>description&#32;constraint'</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `description` | <code>string</code> |  |
| `constraint'` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks


 Non-diagnostic: it reaches inspection, documentation, and generated schema prose, but never a violation and
 never the constraint&#39;s logical meaning. Use <code>custom</code> or <code>customWith</code> to change what a failure says.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">between</span> <span class="n">0</span> <span class="n">10</span> <span class="o">|&gt;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">describe</span> <span class="s">&quot;Retries before the call is abandoned.&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L281-281)
