---
title: "Flow.Process.stream"
linkTitle: "stream"
weight: 2502
---

 Streams process events in the current Flow runtime. The last event is <c>Completed</c>.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.stream&#32;<span>specification</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `specification` | <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../stream/t-flow-flowstream.md">FlowStream</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processevent.md">ProcessEvent</a></span>&gt;</span></code> |  |
