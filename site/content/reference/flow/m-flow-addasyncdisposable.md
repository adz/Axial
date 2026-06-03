---
title: "Flow.addAsyncDisposable"
linkTitle: "addAsyncDisposable"
weight: 2402
type: docs
---

Registers an asynchronously disposable resource with the current runtime scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.addAsyncDisposable&#32;<span>resource</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `resource` | <code><a href="https://learn.microsoft.com/dotnet/api/system.iasyncdisposable">IAsyncDisposable</a></code> | The async disposable resource to close when the current scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that registers the resource. |
