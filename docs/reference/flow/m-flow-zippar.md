---
title: "Flow.zipPar"
linkTitle: "zipPar"
weight: 2500
---

Combines two flows into a tuple of their values, running them concurrently.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.zipPar&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'left</span>&gt;</span></code> | The first flow to combine. |
| `right` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'right</span>&gt;</span></code> | The second flow to combine. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>(<span>'left&#32;*&#32;'right</span>)</span></span>&gt;</span></code> | A flow that returns a tuple of both successful values. |

## Remarks


 If either flow fails, the other is interrupted immediately.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">combined</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">zipPar</span> <span class="id">flow1</span> <span class="id">flow2</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val combined: obj</div>
