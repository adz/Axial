---
title: "Flow.FlowStream.runForEach"
linkTitle: "runForEach"
weight: 2102
type: docs
---

Executes the stream and performs a synchronous action for each successful value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.FlowStream.runForEach&#32;<span>environment&#32;action&#32;arg3</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment required to execute the stream. |
| `action` | <code><span>'value&#32;->&#32;unit</span></code> | The function to execute for each value emitted by the stream. |
| `arg2` | <code><span><a href="t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that represents the execution of the stream. If the stream fails, the flow fails with the same cause. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">stream</span> <span class="o">=</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">fromSeq</span> <span class="pn">[</span><span class="s">&quot;a&quot;</span><span class="pn">;</span> <span class="s">&quot;b&quot;</span><span class="pn">;</span> <span class="s">&quot;c&quot;</span><span class="pn">]</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">flow</span> <span class="o">=</span> <span class="id">FlowStream</span><span class="pn">.</span><span class="id">runForEach</span> <span class="pn">(</span><span class="pn">)</span> <span class="pn">(</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">printfn</span> <span class="s">&quot;%s&quot;</span><span class="pn">)</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">stream</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val stream: obj</div>
<div popover class="fsdocs-tip" id="fs2">val flow: obj</div>
<div popover class="fsdocs-tip" id="fs3">val printfn: format: Printf.TextWriterFormat&lt;&#39;T&gt; -&gt; &#39;T</div>
