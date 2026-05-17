---
title: "IHas"
linkTitle: "IHas<service>"
---

Compatibility contract for a single dependency.

## Signature

<div class="fsdocs-usage">
<code>type IHas<'service></code>
</div>

## Type Parameters

| Name |
| --- |
| `service` |

## Remarks


 Prefer nominal capability interfaces for public workflows. This helper remains for lower-level
 binding machinery that still needs to work with a single dependency directly.
 

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">type</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="if">IClock</span> <span class="o">=</span>
     <span class="k">abstract</span> <span class="fn">UtcNow</span> <span class="pn">:</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="rt">unit</span> <span class="k">-&gt;</span> <span class="id">DateTimeOffset</span>

 <span class="k">type</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="rt">Runtime</span> <span class="o">=</span>
     <span class="pn">{</span> <span class="prop">Clock</span> <span class="pn">:</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="if">IClock</span> <span class="pn">}</span>

 <span class="k">let</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="id">readClock</span> <span class="pn">:</span> <span class="id">Flow</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="6" class="id">Runtime</span><span class="pn">,</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="7" class="id">unit</span><span class="pn">,</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="8" class="id">IClock</span><span class="pn">&gt;</span> <span class="o">=</span>
     <span class="id">flow</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">clock</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">read</span> <span class="id">_</span><span class="pn">.</span><span class="id">Clock</span>
         <span class="k">return</span> <span class="id">clock</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">type IClock =
  abstract UtcNow: unit -&gt; &#39;a</div>
<div popover class="fsdocs-tip" id="fs2">type unit = Unit</div>
<div popover class="fsdocs-tip" id="fs3">type Runtime =
  { Clock: IClock }</div>
<div popover class="fsdocs-tip" id="fs4">val readClock: obj</div>



