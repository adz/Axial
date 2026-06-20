---
title: "Flow.Flow.Runtime.timeout"
linkTitle: "timeout"
weight: 2007
---

Fails with the supplied typed error when the flow does not complete before the timeout.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.timeout&#32;<span>after&#32;timeoutError&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `after` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> | The timeout duration. |
| `timeoutError` | <code>'error</code> | The typed error returned when the timeout wins. |
| `flow` | <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that returns the source outcome or the timeout error. |
