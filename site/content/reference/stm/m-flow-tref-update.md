---
title: "Flow.TRef.update"
linkTitle: "update"
weight: 2103
type: docs
---

Updates the value of the transactional reference within a transaction using the supplied function.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.TRef.update&#32;<span>f&#32;tref</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `f` | <code><span>'T&#32;->&#32;'T</span></code> | The function to apply to the current value to produce the new value. |
| `tref` | <code><span><a href="/reference/Axial/axial-flow-tref-1.html">TRef</a>&lt;'T&gt;</span></code> | The transactional reference to update. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-stm-1.html">STM</a>&lt;unit&gt;</span></code> | An STM operation that updates the reference value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">tx</span> <span class="pn">(</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="fn">counter</span><span class="pn">:</span> <span class="id">TRef</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">int</span><span class="pn">&gt;</span><span class="pn">)</span> <span class="o">=</span> <span class="id">stm</span> <span class="pn">{</span>
     <span class="k">do!</span> <span class="id">TRef</span><span class="pn">.</span><span class="id">update</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">n</span> <span class="k">-&gt;</span> <span class="id">n</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="4" class="id">counter</span>
 <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val tx: counter: &#39;a -&gt; &#39;b</div>
<div popover class="fsdocs-tip" id="fs2">val counter: &#39;a</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
