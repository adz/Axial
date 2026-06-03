---
title: "Flow.race"
linkTitle: "race"
weight: 2501
type: docs
---

Runs two flows concurrently and returns the result of the first one to complete.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.race&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The first flow to run. |
| `right` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The second flow to run. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow containing the result of the first flow to complete. |

## Remarks


 The &quot;loser&quot; flow is interrupted immediately.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">fastOrSlow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">race</span> <span class="id">fastFlow</span> <span class="id">slowFlow</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val fastOrSlow: obj</div>
