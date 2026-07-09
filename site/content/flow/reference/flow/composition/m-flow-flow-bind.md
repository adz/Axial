---
title: "Flow.bind"
linkTitle: "bind"
weight: 2319
type: docs
---

Sequences a dependent flow after a successful value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.bind&#32;<span>binder&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'value&#32;->&#32;<span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'next</span>&gt;</span></span></code> | A function that takes the successful value and returns a new flow. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow to sequence. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'next</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.flow-3">Flow</a> representing the combined workflow. |

## Remarks


 This is the flatmap operation for <a href="https://learn.microsoft.com/dotnet/api/axial.flow-3">Flow</a>. The continuation only runs
 when the source flow succeeds, and it receives the successful value. Use <code>bind</code> when the
 next effect depends on the previous result; use <code>map</code> when the next step is pure.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">1</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">bind</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="pn">(</span><span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
