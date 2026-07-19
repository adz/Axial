---
title: "Schema.Http.AspNetCore.SchemaResult.handleParsed"
linkTitle: "handleParsed"
weight: 2703
type: docs
---

Runs the handler with the trusted model, or short-circuits to the problem-details response.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.SchemaResult.handleParsed&#32;<span>handler&#32;parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `handler` | <code><span>'model&#32;->&#32;<span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a>&gt;</span></span></code> |  |
| `parsed` | <code><span><a href="../../interpreters/t-schema-retainedparseresult.md">RetainedParseResult</a>&lt;<span>'model,&#32;<a href="../../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a>&gt;</span></code> |  |
