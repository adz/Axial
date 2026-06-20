---
title: "Flow.Flow.addDisposable"
linkTitle: "addDisposable"
weight: 2401
---

Registers a disposable resource with the current runtime scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.addDisposable&#32;<span>resource</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `resource` | <code><a href="https://learn.microsoft.com/dotnet/api/system.idisposable">IDisposable</a></code> | The disposable resource to close when the current scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that registers the resource. |
