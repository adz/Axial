---
title: "STM.atomically"
linkTitle: "atomically"
weight: 2104
---


 Executes an STM transaction atomically within a flow while preserving retry/orElse coordination.


## Signature

<div class="fsdocs-usage">
<code><span>STM.atomically&#32;<span>transaction</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `transaction` | <code><span><a href="t-stm.md">STM</a>&lt;'T&gt;</span></code> | The STM transaction to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;'T</span>&gt;</span></code> | A flow that performs the transaction and returns its result. |

## Remarks


 FsFlow&#39;s STM uses a global synchronizing lock to ensure atomicity and coordinate retries.
 While this avoids complex optimistic concurrency control, it means that transactions
 are mutually exclusive and high contention can impact throughput.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">transfer</span> <span class="pn">(</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="fn">fromAcc</span><span class="pn">:</span> <span class="id">TRef</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">int</span><span class="pn">&gt;</span><span class="pn">)</span> <span class="pn">(</span><span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="4" class="fn">toAcc</span><span class="pn">:</span> <span class="id">TRef</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="5" class="id">int</span><span class="pn">&gt;</span><span class="pn">)</span> <span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="6" class="fn">amount</span> <span class="o">=</span>
     <span class="id">stm</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">bal</span> <span class="o">=</span> <span class="id">TRef</span><span class="pn">.</span><span class="id">get</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="7" class="id">fromAcc</span>
         <span class="k">if</span> <span class="id">bal</span> <span class="pn">&lt;</span> <span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="8" class="id">amount</span> <span class="k">then</span> <span class="k">do!</span> <span class="id">STM</span><span class="pn">.</span><span class="id">retry</span>
         <span class="k">do!</span> <span class="id">TRef</span><span class="pn">.</span><span class="id">set</span> <span class="pn">(</span><span class="id">bal</span> <span class="o">-</span> <span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="9" class="id">amount</span><span class="pn">)</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="10" class="id">fromAcc</span>
         <span class="k">do!</span> <span class="id">TRef</span><span class="pn">.</span><span class="id">update</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">b</span> <span class="k">-&gt;</span> <span class="id">b</span> <span class="o">+</span> <span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="11" class="id">amount</span><span class="pn">)</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="12" class="id">toAcc</span>
     <span class="pn">}</span>

 <span class="k">let</span> <span data-fsdocs-tip="fs6" data-fsdocs-tip-unique="13" class="id">flow</span> <span class="o">=</span> <span class="id">STM</span><span class="pn">.</span><span class="id">atomically</span> <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="14" class="id">transfer</span> <span class="id">acc1</span> <span class="id">acc2</span> <span class="n">100</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val transfer: fromAcc: &#39;a -&gt; toAcc: &#39;b -&gt; amount: &#39;c -&gt; &#39;d</div>
<div popover class="fsdocs-tip" id="fs2">val fromAcc: &#39;a</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
<div popover class="fsdocs-tip" id="fs4">val toAcc: &#39;b</div>
<div popover class="fsdocs-tip" id="fs5">val amount: &#39;c</div>
<div popover class="fsdocs-tip" id="fs6">val flow: obj</div>
