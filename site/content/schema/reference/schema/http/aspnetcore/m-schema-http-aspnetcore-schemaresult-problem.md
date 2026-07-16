---
title: "Schema.Http.AspNetCore.SchemaResult.problem"
linkTitle: "problem"
weight: 2700
type: docs
---

A 400 <code>application/problem+json</code> response rendering the failed parse&#39;s diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.SchemaResult.problem&#32;<span>parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `parsed` | <code><span><a href="../../interpreters/t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="../../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></code> |  |
