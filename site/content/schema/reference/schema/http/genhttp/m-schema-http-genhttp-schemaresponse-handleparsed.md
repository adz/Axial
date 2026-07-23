---
title: "Schema.Http.GenHttp.SchemaResponse.handleParsed"
linkTitle: "handleParsed"
weight: 2703
type: docs
---

Runs the handler with the trusted model, or short-circuits to the problem-details response.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.SchemaResponse.handleParsed&#32;<span>request&#32;handler&#32;parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a></code> |  |
| `handler` | <code><span>'model&#32;->&#32;<span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;<a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.iresponse">IResponse</a>&gt;</span></span></code> |  |
| `parsed` | <code><span><a href="../../interpreters/t-schema-retainedparseresult.md">RetainedParseResult</a>&lt;'model&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;<a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.iresponse">IResponse</a>&gt;</span></code> |  |
