---
title: "Flow.Hosting.Node.NodeApp.run"
linkTitle: "run"
weight: 2002
---

Starts a Node application and waits for its final exit.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Hosting.Node.NodeApp.run&#32;<span>describeError&#32;environment&#32;application</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `describeError` | <code><span>'error&#32;->&#32;string</span></code> |  |
| `environment` | <code>'env</code> |  |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="../exit/t-flow-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> |  |
