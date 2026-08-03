---
title: "Constraint.customWith"
linkTitle: "customWith"
weight: 2305
type: docs
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


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">customWith</span> <span class="s">&quot;must be a supported currency&quot;</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">code</span> <span class="k">-&gt;</span>
     <span class="k">if</span> <span class="id">supported</span><span class="pn">.</span><span class="id">Contains</span> <span class="id">code</span> <span class="k">then</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Ok</span> <span class="pn">(</span><span class="pn">)</span>
     <span class="k">else</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">Error</span> <span class="pn">(</span><span class="id">Atomic</span> <span class="pn">(</span><span class="id">Expected</span> <span class="pn">(</span><span class="id">MembershipAtom</span> <span class="pn">(</span><span class="id">OneOf</span> <span class="id">choices</span><span class="pn">)</span><span class="pn">,</span> <span class="id">ConstraintValue</span><span class="pn">.</span><span class="id">tryCreate</span> <span class="id">code</span><span class="pn">)</span><span class="pn">)</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L244-244)
