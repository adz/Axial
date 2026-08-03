---
title: "Constraint.guard"
linkTitle: "guard"
weight: 2202
---

Runs a constraint and returns the unchanged value after success.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.guard&#32;<span>constraint'&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="t-constraint-violation.md">Violation</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">value</span> <span class="o">|&gt;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">guard</span> <span class="id">requiredName</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="m">Result</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">mapError</span> <span class="id">NameRejected</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs2">val mapError: mapping: (&#39;TError -&gt; &#39;U) -&gt; result: Result&lt;&#39;T,&#39;TError&gt; -&gt; Result&lt;&#39;T,&#39;U&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L103-103)
