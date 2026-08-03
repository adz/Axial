---
title: "Constraint.customWith"
linkTitle: "customWith"
weight: 2305
---

Runs an arbitrary callback that reports its own violation.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.customWith&#32;<span>description&#32;check</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `description` | <code>string</code> |  |
| `check` | <code><span>'value&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="../result/errors/t-constraint-violation.md">Violation</a></span>&gt;</span></span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks


 Use this when the failure deserves a structured reason a bare predicate cannot give. Because the callback
 supplies only a violation-returning function, <code>test</code> runs it and discards the error, so a failing test
 costs whatever the callback allocates. Returning an <code>Expected</code> leaf makes no false portable claim: the
 enclosing description is still opaque.

 The callback&#39;s shape is exactly <code>Constraint.check</code> applied to a constraint, so the usual way to
 supply a structured reason is to reuse a built-in rather than build a violation by hand.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">customWith</span> <span class="s">&quot;must be a supported currency&quot;</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">check</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">oneOf</span> <span class="id">supported</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L280-280)
