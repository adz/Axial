---
title: "Scope.AddFinalizer"
linkTitle: "AddFinalizer"
weight: 2100
---

Registers an asynchronous finalizer to run when the scope closes.

## Signature

<div class="fsdocs-usage">
<code><span>this.AddFinalizer</span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `finalizer` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The finalizer to run during scope cleanup. |

## Returns

| Type | Description |
| --- | --- |
| <code>unit</code> |  |
