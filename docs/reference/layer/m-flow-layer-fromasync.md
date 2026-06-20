---
title: "Flow.Layer.fromAsync"
linkTitle: "fromAsync"
weight: 2200
---

Creates a layer from a raw async provisioning function.

**Platform:** Fable compatible

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Layer.fromAsync&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><span>'input&#32;*&#32;<a href="/reference/Axial/axial-flow-scope.html">Scope</a></span>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="/reference/Axial/axial-flow-exit-2.html">Exit</a>&lt;<span>'output,&#32;'error</span>&gt;</span>&gt;</span></span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
