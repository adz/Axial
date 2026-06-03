---
title: "Flow.run"
linkTitle: "run"
weight: 2200
type: docs
---

Executes a flow with the provided environment and the default cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.run&#32;<span>environment&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment required by the flow. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | An effect that represents the asynchronous execution outcome. |

## Remarks

Uncaught exceptions become <code>Cause.Die</code>; cancellation becomes <code>Cause.Interrupt</code>.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">read</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">env</span> <span class="k">-&gt;</span> <span class="s">$&quot;Hello, {</span><span class="id">env</span><span class="s">}!&quot;</span><span class="pn">)</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">result</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">run</span> <span class="s">&quot;World&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">flow</span>
 <span class="c">// result = Effect that resolves to Success &quot;Hello, World!&quot; on both .NET and Fable</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
<div popover class="fsdocs-tip" id="fs2">val result: obj</div>
