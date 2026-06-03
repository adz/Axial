---
title: "Validation.fromResult"
linkTitle: "fromResult"
weight: 2105
---

Lifts a standard <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into the <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a> context.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.fromResult&#32;<span>result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The result to lift. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a> mirroring the result. |

## Remarks


 If the result is an error, it is wrapped in a root-level <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-diagnostics-1.html">Diagnostics</a> graph.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="vt">Result</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">Ok</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">fromResult</span> <span class="c">// Validation (Ok 5)</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="4" class="id">v2</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="vt">Result</span><span class="pn">.</span><span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="6" class="id">Error</span> <span class="s">&quot;fail&quot;</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">fromResult</span> <span class="c">// Validation (Error { Errors = [&quot;fail&quot;]; ... })</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs3">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs4">val v2: obj</div>
<div popover class="fsdocs-tip" id="fs5">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
