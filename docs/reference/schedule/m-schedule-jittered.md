---
title: "Schedule.jittered"
linkTitle: "jittered"
weight: 2103
---

Adds random jitter to a schedule&#39;s delay.

## Signature

<div class="fsdocs-usage">
<code><span>Schedule.jittered&#32;<span>arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-schedule-3.html">Schedule</a>&lt;<span>'env,&#32;'input,&#32;'output</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-schedule-3.html">Schedule</a>&lt;<span>'env,&#32;'input,&#32;'output</span>&gt;</span></code> | A new schedule where each delay is multiplied by a random factor between 0.5 and 1.5. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">schedule</span> <span class="o">=</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">spaced</span> <span class="pn">(</span><span class="id">TimeSpan</span><span class="pn">.</span><span class="id">FromSeconds</span> <span class="n">1.0</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">jittered</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val schedule: obj</div>
