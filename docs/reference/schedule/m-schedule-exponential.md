---
title: "Schedule.exponential"
linkTitle: "exponential"
weight: 2102
---

Creates a schedule that recurs with exponential backoff.

## Signature

<div class="fsdocs-usage">
<code><span>Schedule.exponential&#32;<span>baseDelay</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `baseDelay` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> | The initial delay for the first retry. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schedule.md">Schedule</a>&lt;<span>'env,&#32;'input,&#32;<a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></span>&gt;</span></code> | A schedule that recurs indefinitely, doubling the delay each time (baseDelay * 2^attempt). |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">schedule</span> <span class="o">=</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">exponential</span> <span class="pn">(</span><span class="id">TimeSpan</span><span class="pn">.</span><span class="id">FromMilliseconds</span> <span class="n">100.0</span><span class="pn">)</span>
 <span class="c">// Delays: 100ms, 200ms, 400ms, 800ms...</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val schedule: obj</div>
