---
title: "ErrorHandling.Result.orElseWith"
linkTitle: "orElseWith"
weight: 2106
type: docs
---

Computes a fallback result from the source error when the result fails.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.orElseWith&#32;<span>fallback&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code><span>'error&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span></code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Remarks

The lazy counterpart to <code>orElse</code>, matching the <code>Flow.orElseWith</code> naming and shape:
 the fallback runs only on failure and can inspect the error that caused it.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Error</span> <span class="s">&quot;boom&quot;</span>
 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">result</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">Result</span><span class="pn">.</span><span class="id">orElseWith</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">error</span> <span class="k">-&gt;</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="id">Ok</span> <span class="pn">(</span><span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="6" class="id">String</span><span class="pn">.</span><span data-fsdocs-tip="fs6" data-fsdocs-tip-unique="7" class="id">length</span> <span class="id">error</span><span class="pn">)</span><span class="pn">)</span> <span class="c">// Ok 4</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: Result&lt;&#39;a,string&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs4">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs5">module String

from Microsoft.FSharp.Core</div>
<div popover class="fsdocs-tip" id="fs6">val length: str: string -&gt; int</div>
