---
title: "Flow.toResult"
linkTitle: "toResult"
weight: 2210
type: docs
---

Executes a flow and converts the final <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a> into a standard <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a>.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.toResult&#32;<span>environment&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> |  |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Remarks


 Interruption signals and defects are raised as exceptions in the caller&#39;s context.
