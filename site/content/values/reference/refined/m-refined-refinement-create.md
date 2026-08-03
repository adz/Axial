---
title: "Refined.Refinement.create"
linkTitle: "create"
weight: 2602
type: docs
---

Constructs a refined value, reporting why the raw value was not admitted.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.create&#32;<span>refinement&#32;underlying</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `refinement` | <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'underlying,&#32;'refined</span>&gt;</span></code> |  |
| `underlying` | <code>'underlying</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'refined,&#32;<a href="../constraint/t-constraint-violation.md">Violation</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">value</span> <span class="o">|&gt;</span> <span class="id">Refinement</span><span class="pn">.</span><span class="id">create</span> <span class="id">retryCount</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="m">Result</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">mapError</span> <span class="id">InvalidRetryCount</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs2">val mapError: mapping: (&#39;TError -&gt; &#39;U) -&gt; result: Result&lt;&#39;T,&#39;TError&gt; -&gt; Result&lt;&#39;T,&#39;U&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refinement.fs#L51-51)
