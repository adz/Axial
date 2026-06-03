---
title: "Layer.effect"
linkTitle: "effect"
weight: 2200
type: docs
---

Creates a layer from a raw effectful provisioning function.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.effect&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><span>'input&#32;*&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-scope.html">Scope</a></span>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'output,&#32;'error</span>&gt;</span></span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layer-3.html">Layer</a>&lt;<span>'input,&#32;'error,&#32;'output</span>&gt;</span></code> |  |
