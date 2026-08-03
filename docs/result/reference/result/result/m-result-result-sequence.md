---
title: "Result.sequence"
linkTitle: "sequence"
weight: 2301
---

Turns a sequence of results into one fail-fast result containing all successes.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Result.sequence&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'value&#32;list</span>,&#32;'error</span>&gt;</span></code> |  |

## Remarks

Takes any sequence and always produces a list. Stops at the first error.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Ok</span> <span class="n">1</span><span class="pn">;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="uc">Error</span> <span class="s">&quot;missing&quot;</span><span class="pn">;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="uc">Ok</span> <span class="n">3</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">Result</span><span class="pn">.</span><span class="id">sequence</span> <span class="c">// Error &quot;missing&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Result/Result.fs#L223-223)
