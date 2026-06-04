---
title: "Flow.tap"
linkTitle: "tap"
weight: 2316
---

Runs an effect on success and preserves the original value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.tap&#32;<span>binder&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'value&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></span></code> | A function that produces a side-effect flow from the successful value. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> that preserves the original success value after the side effect. |

## Remarks


 Use this for logging, telemetry, metrics, or audit steps that should observe a successful
 value without replacing it. If the <span class="fsdocs-param-name">binder</span> flow fails, that failure becomes
 the result of the whole flow, because the tap effect is still part of the workflow.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="n">42</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">tap</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="pn">(</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>
