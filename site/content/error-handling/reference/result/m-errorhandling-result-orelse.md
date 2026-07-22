---
title: "ErrorHandling.Result.orElse"
linkTitle: "orElse"
weight: 2105
type: docs
---

Falls back to another result when the source result fails.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.orElse&#32;<span>fallback&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Error</span> <span class="s">&quot;boom&quot;</span>
 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">result</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">Result</span><span class="pn">.</span><span class="id">orElse</span> <span class="pn">(</span><span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="id">Ok</span> <span class="n">5</span><span class="pn">)</span> <span class="c">// Ok 5</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: Result&lt;&#39;a,string&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs4">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
