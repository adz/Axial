---
title: "Flow.Repeat"
linkTitle: "Repeat"
weight: 2501
---

Repeats a successful flow according to the supplied schedule.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Repeat<span><span>(<span>flow,&#32;schedule</span>)</span></span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to repeat if it succeeds. |
| `schedule` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-schedule-3.html">Schedule</a>&lt;<span>'env,&#32;'value,&#32;'output</span>&gt;</span></code> | The schedule that determines when and if to repeat based on the successful value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that repeats the original flow according to the schedule, returning the last successful value when it stops. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">work</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">ok</span> <span class="n">42</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">repeated</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">Repeat</span><span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">work</span><span class="pn">,</span> <span class="id">Schedule</span><span class="pn">.</span><span class="id">recurs</span> <span class="n">5</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val work: obj</div>
<div popover class="fsdocs-tip" id="fs2">val repeated: obj</div>
