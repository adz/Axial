---
title: "Flow.TRef.make"
linkTitle: "make"
weight: 2100
---

Creates a new <a href="https://learn.microsoft.com/dotnet/api/axial.tref-1">TRef</a> with the initial value within an STM transaction.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.TRef.make&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'T</code> | The initial value for the transactional reference. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-stm-1.html">STM</a>&lt;<span><a href="/reference/Axial/axial-flow-tref-1.html">TRef</a>&lt;'T&gt;</span>&gt;</span></code> | An STM operation that, when executed, produces a new transactional reference. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">tx</span> <span class="o">=</span> <span class="id">stm</span> <span class="pn">{</span>
     <span class="k">let!</span> <span class="id">counter</span> <span class="o">=</span> <span class="id">TRef</span><span class="pn">.</span><span class="id">make</span> <span class="n">0</span>
     <span class="k">return</span> <span class="id">counter</span>
 <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val tx: obj</div>
