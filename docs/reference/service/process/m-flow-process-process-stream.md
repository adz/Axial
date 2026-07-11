---
title: "Flow.Process.stream"
linkTitle: "stream"
weight: 2504
---

 Streams structured process events with one-element bounded backpressure. The last event is <c>Completed</c>.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.stream&#32;<span>pipeline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pipeline` | <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flowstream-3.html">FlowStream</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processevent.md">ProcessEvent</a></span>&gt;</span></code> |  |
