---
title: "Flow.Runtime.timeoutToError"
linkTitle: "timeoutToError"
weight: 2009
type: docs
---

Alias for <code>timeout</code> that emphasizes typed failure on timeout.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.timeoutToError&#32;<span>after&#32;error&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `after` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |
| `error` | <code>'error</code> |  |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |
