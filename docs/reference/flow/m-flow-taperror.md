---
title: "Flow.tapError"
linkTitle: "tapError"
weight: 2314
---

Runs a synchronous side effect on failure and preserves the original error.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.tapError&#32;<span>binder&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'error&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></span></code> | A function that produces a side-effect flow from the error value. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> that preserves the original error after the side effect. |

## Remarks


 Use this for error logging or cleanup actions that depend on the environment.
 If the <span class="fsdocs-param-name">binder</span> side-effect flow itself fails, its error will
 overwrite the original error.
 

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fail</span> <span class="s">&quot;error&quot;</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">tapError</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">err</span> <span class="k">-&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">succeed</span> <span class="pn">(</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flow: obj</div>



