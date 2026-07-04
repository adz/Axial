---
title: "Flow.Flow.Runtime.sleep"
linkTitle: "sleep"
weight: 2103
---

Suspends the flow for the specified duration, observing cancellation.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.sleep&#32;<span>delay</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `delay` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> | The duration to sleep. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that completes after the specified delay. |
