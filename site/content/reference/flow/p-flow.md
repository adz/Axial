---
title: "flow"
linkTitle: "flow { }"
weight: 2000
type: docs
---


 The universal <code>flow { }</code> computation expression.


## Signature

<div class="fsdocs-usage">
<code><span>flow&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="builders-flow.md">FlowBuilder</a></code> | A <a href="builders-flow.md">FlowBuilder</a> instance. |

## Remarks

<p class='fsdocs-para'>
 Use this builder when the boundary can mix synchronous values, <code>Async</code>, <code>Task</code>,
 <code>Result</code>, and environment requests while keeping typed failures and explicit
 dependency access.
 </p><p class='fsdocs-para'>
 It preserves the current environment model while allowing the workflow to compose
 task-oriented inputs directly, so callers do not need to switch builders just to cross
 an async boundary.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">greeting</span> <span class="o">=</span>
     <span class="id">flow</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">name</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">env</span>
         <span class="k">let!</span> <span class="id">suffix</span> <span class="o">=</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">async</span> <span class="pn">{</span> <span class="k">return</span> <span class="s">&quot;!&quot;</span> <span class="pn">}</span>
         <span class="k">return</span> <span class="s">$&quot;Hello, {</span><span class="id">name</span><span class="s">}{</span><span class="id">suffix</span><span class="s">}&quot;</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val greeting: obj</div>
<div popover class="fsdocs-tip" id="fs2">val async: AsyncBuilder</div>
