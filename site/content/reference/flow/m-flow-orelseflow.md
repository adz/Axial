---
title: "Flow.orElseFlow"
linkTitle: "orElseFlow"
weight: 2308
type: docs
---

Turns a pure validation result into a synchronous flow with environment-provided failure.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.orElseFlow&#32;<span>errorFlow&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `errorFlow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'error</span>&gt;</span></code> | A flow that reads the environment to produce an error value. |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;unit</span>&gt;</span></code> | The pure result to bridge. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> that mirrors the success of the result or fails with the outcome of the error flow. |

## Remarks


 This helper bridges the gap between pure validation (which often uses <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> or <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>)
 and the <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> environment model. If the result is an error, the provided <span class="fsdocs-param-name">errorFlow</span>
 is executed to produce the final application error.


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
