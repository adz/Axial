---
title: "Schema.Http.GenHttp.SchemaRequest.json"
linkTitle: "json"
weight: 2600
---

Parses the JSON request body through the schema; a missing body parses as missing input.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.SchemaRequest.json&#32;<span>schema&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `request` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;<span><a href="../../interpreters/t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="../../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span>&gt;</span></code> |  |
