---
title: "Refined.Refine.from"
linkTitle: "from"
weight: 2900
---


 Resolves the <code>Refinement</code> definition for the raw value and expected destination type, then runs its smart
 constructor. A destination type participates by defining a static <code>Refinement</code> member.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refine.from&#32;<span>raw</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `raw` | <code>^raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>^value,&#32;<a href="../types/t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">id</span> <span class="pn">:</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="vt">Result</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="vt">int</span><span class="pn">,</span> <span class="id">RefinementError</span><span class="pn">&gt;</span> <span class="o">=</span> <span class="id">Refine</span><span class="pn">.</span><span class="id">from</span> <span class="s">&quot;42&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val id: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />module Result

from Microsoft.FSharp.Core<br /><br />--------------------<br />
type Result&lt;&#39;T,&#39;TError&gt; =
  | Ok of ResultValue: &#39;T
  | Error of ErrorValue: &#39;TError</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
