---
title: "Flow.toAsync"
linkTitle: "toAsync"
weight: 2202
---

Executes a flow and returns an async that resolves to the final exit outcome, observing the ambient cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.toAsync&#32;<span>environment&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment required by the flow. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | An async that completes with the fiber&#39;s final outcome. |
