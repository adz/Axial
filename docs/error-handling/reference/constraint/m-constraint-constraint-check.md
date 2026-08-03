---
title: "Constraint.check"
linkTitle: "check"
weight: 2201
---

Runs a constraint, returning why the value failed.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.check&#32;<span>constraint'&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="../result/errors/t-constraint-violation.md">Violation</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">retryCount</span> <span class="o">=</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">between</span> <span class="n">0</span> <span class="n">10</span>
 <span class="n">42</span> <span class="o">|&gt;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">check</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">retryCount</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="3" class="m">Result</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">mapError</span> <span class="id">Violation</span><span class="pn">.</span><span class="id">render</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val retryCount: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs3">val mapError: mapping: (&#39;TError -&gt; &#39;U) -&gt; result: Result&lt;&#39;T,&#39;TError&gt; -&gt; Result&lt;&#39;T,&#39;U&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L97-97)
