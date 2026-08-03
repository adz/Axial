---
title: "Flow.orElseFlow"
linkTitle: "orElseFlow"
weight: 2315
type: docs
---

Attaches an environment-derived error to a result that failed without one.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.orElseFlow&#32;<span>errorFlow&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `errorFlow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'error</span>&gt;</span></code> | A flow that reads the environment to produce an error value. |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;unit</span>&gt;</span></code> | The pure result to bridge. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.flow-3">Flow</a> that mirrors the success of the result or fails with the outcome of the error flow. |

## Remarks

<p class='fsdocs-para'>
 The <code>unit</code> error is not an empty error type — it is the absence of a reason. <code>Result.okIf</code> and
 <code>Result.failIf</code> report that a value failed a predicate and deliberately nothing else, leaving the reason
 to a separate step. <code>Result.orError</code> is that step for a constant; this is that step when producing the
 error needs the environment, as a localized message, a correlation id, or a configured code does.
 </p><p class='fsdocs-para'>
 Pinning the source to <code>unit</code> is what makes <span class="fsdocs-param-name">errorFlow</span> the only possible source of the
 error. A result that already carries one keeps it: map it with <code>Result.mapError</code> and use
 <code>Flow.fromResult</code>.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="vt">Result</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">Error</span> <span class="pn">(</span><span class="pn">)</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="4" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">orElseFlow</span> <span class="pn">(</span><span class="id">Flow</span><span class="pn">.</span><span class="id">read</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">env</span> <span class="k">-&gt;</span> <span class="s">&quot;error&quot;</span><span class="pn">)</span><span class="pn">)</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="5" class="id">result</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: Result&lt;&#39;a,unit&gt;</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs3">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs4">val flow: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Flow/Flow.fs#L970-970)
