---
title: "Result.traverse"
linkTitle: "traverse"
weight: 2300
type: docs
---

Maps each value with a result-returning function, stopping at the first error.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Result.traverse&#32;<span>mapping&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapping` | <code><span>'input&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></span></code> |  |
| `values` | <code><span>'input&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'output&#32;list</span>,&#32;'error</span>&gt;</span></code> |  |

## Remarks

Takes any sequence and always produces a list. Traversal stops at the first error, so later
 mappings do not run. Use one of the accumulating builders when every error should be reported.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="s">&quot;1&quot;</span><span class="pn">;</span> <span class="s">&quot;2&quot;</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Result</span><span class="pn">.</span><span class="id">traverse</span> <span class="id">parseInt</span> <span class="c">// Ok [ 1; 2 ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Result/Result.fs#L205-205)
