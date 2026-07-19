---
title: "Schema.Http.AspNetCore.SchemaRequest.form"
linkTitle: "form"
weight: 2601
type: docs
---

Parses the posted form through the schema; dotted field names such as <code>address.street</code> nest.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.SchemaRequest.form&#32;<span>schema&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `request` | <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httprequest">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<span><a href="../../interpreters/t-schema-retainedparseresult.md">RetainedParseResult</a>&lt;<span>'model,&#32;<a href="../../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span>&gt;</span></code> |  |
