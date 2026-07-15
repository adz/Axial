---
title: "Flow.Schedule.jitteredWith"
linkTitle: "jitteredWith"
weight: 2104
type: docs
---

Adds jitter to a schedule&#39;s delay using a caller-supplied sample source.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Schedule.jitteredWith&#32;<span>sample&#32;arg2</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `sample` | <code><span>unit&#32;->&#32;float</span></code> | A function returning a value in [0.0, 1.0), sampled once per attempt. Supply a deterministic function for reproducible schedules and tests. |
| `arg1` | <code><span><a href="t-flow-schedule.md">Schedule</a>&lt;<span>'env,&#32;'input,&#32;'output</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-schedule.md">Schedule</a>&lt;<span>'env,&#32;'input,&#32;'output</span>&gt;</span></code> | A new schedule where each delay is multiplied by <code>sample () + 0.5</code>, giving a factor between 0.5 and 1.5, capped at <a href="https://learn.microsoft.com/dotnet/api/system.timespan.maxvalue">TimeSpan.MaxValue</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">schedule</span> <span class="o">=</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">spaced</span> <span class="pn">(</span><span class="id">TimeSpan</span><span class="pn">.</span><span class="id">FromSeconds</span> <span class="n">1.0</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">jitteredWith</span> <span class="pn">(</span><span class="k">fun</span> <span class="pn">(</span><span class="pn">)</span> <span class="k">-&gt;</span> <span class="n">0.25</span><span class="pn">)</span>
 <span class="c">// Every delay becomes 750ms.</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val schedule: obj</div>
