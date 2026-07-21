---
title: "Flow.Schedule.retry"
linkTitle: "retry"
weight: 2105
---

Retries a failing flow according to the supplied schedule.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Schedule.retry&#32;<span>schedule&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schedule` | <code><span><a href="t-flow-schedule.md">Schedule</a>&lt;<span>'env,&#32;'error,&#32;'output</span>&gt;</span></code> | The schedule that determines when and if to retry based on the error. |
| `flow` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to retry if it fails. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that will retry the original flow according to the schedule until it succeeds or the schedule stops. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">flakyWork</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">fail</span> <span class="s">&quot;oops&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">retried</span> <span class="o">=</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">flakyWork</span> <span class="o">|&gt;</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">retry</span> <span class="pn">(</span><span class="id">Schedule</span><span class="pn">.</span><span class="id">recurs</span> <span class="n">3</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val flakyWork: obj</div>
<div popover class="fsdocs-tip" id="fs2">val retried: obj</div>
