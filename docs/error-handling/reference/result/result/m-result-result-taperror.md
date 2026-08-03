---
title: "Result.tapError"
linkTitle: "tapError"
weight: 2501
---

Runs a side effect on the error value and returns the result unchanged.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Result.tapError&#32;<span>effect&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `effect` | <code><span>'error&#32;->&#32;unit</span></code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Error</span> <span class="s">&quot;boom&quot;</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">Result</span><span class="pn">.</span><span class="id">tapError</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">printfn</span> <span class="s">&quot;failed: %s&quot;</span><span class="pn">)</span> <span class="c">// prints, then returns Error &quot;boom&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs3">val printfn: format: Printf.TextWriterFormat&lt;&#39;T&gt; -&gt; &#39;T</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Result/Result.fs#L190-190)
