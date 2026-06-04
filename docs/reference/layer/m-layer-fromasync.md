---
title: "Layer.fromAsync"
linkTitle: "fromAsync"
weight: 2200
---

Creates a layer from a raw async provisioning function.

**Platform:** Fable compatible

## Signature

<div class="fsdocs-usage">
<code><span>Layer.fromAsync&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><span>'input&#32;*&#32;<a href="../scope/t-scope.md">Scope</a></span>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="../exit/t-exit.md">Exit</a>&lt;<span>'output,&#32;'error</span>&gt;</span>&gt;</span></span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
