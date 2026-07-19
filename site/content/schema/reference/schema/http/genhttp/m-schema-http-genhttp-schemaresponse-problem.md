---
title: "Schema.Http.GenHttp.SchemaResponse.problem"
linkTitle: "problem"
weight: 2700
type: docs
---

A 400 <code>application/problem+json</code> response rendering the failed parse&#39;s diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.SchemaResponse.problem&#32;<span>request&#32;parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a></code> |  |
| `parsed` | <code><span><a href="../../interpreters/t-schema-retainedparseresult.md">RetainedParseResult</a>&lt;<span>'model,&#32;<a href="../../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.iresponse">IResponse</a></code> |  |
