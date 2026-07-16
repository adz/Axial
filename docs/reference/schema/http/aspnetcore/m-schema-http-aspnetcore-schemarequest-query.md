---
title: "Schema.Http.AspNetCore.SchemaRequest.query"
linkTitle: "query"
weight: 2602
---

Parses the query string through the schema.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.SchemaRequest.query&#32;<span>schema&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `request` | <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httprequest">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../interpreters/t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="../../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |
