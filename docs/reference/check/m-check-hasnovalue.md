---
title: "Check.hasNoValue"
linkTitle: "hasNoValue"
weight: 2407
---

Returns success when the nullable has no value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.hasNoValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when <code>HasValue</code> is false; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Nullable</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">int</span><span class="pn">&gt;</span><span class="pn">(</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">hasNoValue</span> <span class="c">// Ok ()</span>
 <span class="id">Nullable</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">hasNoValue</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
