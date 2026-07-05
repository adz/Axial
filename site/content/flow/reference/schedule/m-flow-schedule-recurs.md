---
title: "Flow.Schedule.recurs"
linkTitle: "recurs"
weight: 2100
type: docs
---

Creates a schedule that recurs a fixed number of times.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Schedule.recurs&#32;<span>n</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `n` | <code>int</code> | The maximum number of times to recur. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-schedule.md">Schedule</a>&lt;<span>'env,&#32;'input,&#32;int</span>&gt;</span></code> | A schedule that recurs up to <span class="fsdocs-param-name">n</span> times, emitting the current attempt count (0 to n-1). |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">schedule</span> <span class="o">=</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">recurs</span> <span class="n">3</span>
 <span class="c">// Will run for attempts 0, 1, 2 and then stop.</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val schedule: obj</div>
