---
title: "Flow.Schedule.spaced"
linkTitle: "spaced"
weight: 2101
---

Creates a schedule that recurs with a fixed delay between attempts.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Schedule.spaced&#32;<span>delay</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `delay` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> | The fixed time span to wait between each attempt. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-schedule-3.html">Schedule</a>&lt;<span>'env,&#32;'input,&#32;int</span>&gt;</span></code> | A schedule that recurs indefinitely with the specified fixed delay, emitting the current attempt count. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">schedule</span> <span class="o">=</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">spaced</span> <span class="pn">(</span><span class="id">TimeSpan</span><span class="pn">.</span><span class="id">FromSeconds</span> <span class="n">1.0</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val schedule: obj</div>
