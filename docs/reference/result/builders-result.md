---
title: "result"
linkTitle: "result { }"
weight: 2000
---


 The fail-fast <code>result { }</code> computation expression.


## Signature

<div class="fsdocs-usage">
<code><span>result&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="builders-result.md">ResultBuilder</a></code> | A <a href="builders-result.md">ResultBuilder</a> instance. |

## Remarks

<p class='fsdocs-para'>
 Use this builder when the happy path should short-circuit on the first error
 and you want to keep the workflow in <code>Result</code> shape all the way through.
 </p><p class='fsdocs-para'>
 It works well for parsing, validation, and other boundaries where failure is expected
 to stop the flow immediately instead of accumulating diagnostics.
 </p><p class='fsdocs-para'>
 Use <code>Check.orError</code> when a pure check needs a domain error, and <code>Guard.MapError</code> when
 you need to remap an existing error before entering the CE.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">parsedUser</span> <span class="o">=</span>
     <span class="id">result</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">age</span> <span class="o">=</span> <span class="id">parseAge</span> <span class="id">input</span>
         <span class="k">let!</span> <span class="id">name</span> <span class="o">=</span> <span class="id">parseName</span> <span class="id">input</span>
         <span class="k">return</span> <span class="pn">{</span> <span class="id">Age</span> <span class="o">=</span> <span class="id">age</span><span class="pn">;</span> <span class="id">Name</span> <span class="o">=</span> <span class="id">name</span> <span class="pn">}</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val parsedUser: obj</div>
