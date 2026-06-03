---
title: "Flow.Runtime.timeoutWith"
linkTitle: "timeoutWith"
weight: 2011
type: docs
---

Runs a fallback flow when the source flow does not complete before the timeout.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.timeoutWith&#32;<span>after&#32;fallback&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `after` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> | The timeout duration. |
| `fallback` | <code><span>unit&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></span></code> | Creates the fallback flow when the timeout wins. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that returns the source outcome or the fallback outcome. |
