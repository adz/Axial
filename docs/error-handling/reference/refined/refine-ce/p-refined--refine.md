---
title: "Refined..refine"
linkTitle: "refine"
weight: 3000
---


 The fail-fast <code>refine { }</code> computation expression. A raw value can be parsed or refined according to the
 type annotation on the left side of <code>let!</code>; explicit <code>Parse</code> and <code>Refine</code> results also bind directly.


## Signature

<div class="fsdocs-usage">
<code><span>refine&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="p-refined--refine.md">RefineBuilder</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">create</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="fn">rawId</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="fn">rawName</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="4" class="fn">rawQuantity</span> <span class="o">=</span>
     <span class="id">refine</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="pn">(</span><span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="5" class="id">id</span><span class="pn">:</span> <span data-fsdocs-tip="fs6" data-fsdocs-tip-unique="6" class="id">int</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="7" class="id">rawId</span>
         <span class="k">let!</span> <span class="pn">(</span><span class="id">name</span><span class="pn">:</span> <span class="id">NonBlankString</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="8" class="id">rawName</span>
         <span class="k">let!</span> <span class="pn">(</span><span class="id">quantity</span><span class="pn">:</span> <span class="id">PositiveInt</span><span class="pn">)</span> <span class="o">=</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="9" class="id">rawQuantity</span>
         <span class="k">return</span> <span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="10" class="id">id</span><span class="pn">,</span> <span class="id">name</span><span class="pn">,</span> <span class="id">quantity</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val create: rawId: &#39;a -&gt; rawName: &#39;b -&gt; rawQuantity: &#39;c -&gt; &#39;d</div>
<div popover class="fsdocs-tip" id="fs2">val rawId: &#39;a</div>
<div popover class="fsdocs-tip" id="fs3">val rawName: &#39;b</div>
<div popover class="fsdocs-tip" id="fs4">val rawQuantity: &#39;c</div>
<div popover class="fsdocs-tip" id="fs5">val id: x: &#39;T -&gt; &#39;T</div>
<div popover class="fsdocs-tip" id="fs6">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
