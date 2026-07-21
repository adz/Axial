---
title: "Flow.App.run"
linkTitle: "run"
weight: 2102
type: docs
---

Runs a root workflow to completion using the caller&#39;s asynchronous cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.App.run&#32;<span>environment&#32;application</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The explicit environment supplied to the root workflow. |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The root workflow to run. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="../exit/t-flow-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | The final exit after the root scope has closed. |
