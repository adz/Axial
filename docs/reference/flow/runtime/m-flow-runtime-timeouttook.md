---
title: "Flow.Runtime.timeoutToOk"
linkTitle: "timeoutToOk"
weight: 2008
---

Returns the supplied success value when the flow does not complete before the timeout.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.timeoutToOk&#32;<span>after&#32;value&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `after` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> | The timeout duration. |
| `value` | <code>'value</code> | The success value returned when the timeout wins. |
| `flow` | <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that returns the source outcome or the supplied success value. |
