---
title: "Schema.Http.GenHttp.SchemaResponse.codec"
linkTitle: "codec"
weight: 2701
---

A JSON response rendering the trusted model through the compiled codec.

## Signature

<div class="fsdocs-usage">
<code><span>SchemaResponse.codec&#32;<span>Schema.Http.GenHttp.SchemaResponse.codec&#32;status&#32;request&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `codec` | <code><span><a href="../../../codec/t-schema-codec-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> |  |
| `status` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.responsestatus">ResponseStatus</a></code> |  |
| `request` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a></code> |  |
| `value` | <code>'model</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.iresponse">IResponse</a></code> |  |
