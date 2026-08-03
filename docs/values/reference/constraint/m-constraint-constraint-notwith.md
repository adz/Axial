---
title: "Constraint.notWith"
linkTitle: "notWith"
weight: 2303
---


 Negates a constraint. The result is opaque: it runs normally but cannot be exported or proved, and reports
 the supplied prose.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.notWith&#32;<span>description&#32;constraint'</span></span></code>
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


 The prose is required because there is no honest general complement to derive one from. Membership, format,
 uniqueness, and numeric families have no complement inside their family; float comparisons are not
 complementable under <code>NaN</code>, where both <code>x &gt; y</code> and <code>x &lt;= y</code> are false; and a cardinality
 complement would need bounds this catalogue rejects, such as a maximum of -1. An operation that is
 sometimes interpreted, sometimes needs prose, and sometimes cannot be constructed is worse than one that is
 honestly opaque.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">notWith</span> <span class="s">&quot;must not be a reserved name&quot;</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">oneOf</span> <span class="pn">[</span> <span class="s">&quot;admin&quot;</span><span class="pn">;</span> <span class="s">&quot;root&quot;</span> <span class="pn">]</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L202-202)
