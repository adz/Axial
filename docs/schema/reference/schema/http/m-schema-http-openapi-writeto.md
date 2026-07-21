---
title: "Schema.Http.OpenApi.writeTo"
linkTitle: "writeTo"
weight: 2303
---

Writes an OpenAPI 3.1 JSON document covering the supplied endpoints to a stream.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.OpenApi.writeTo&#32;<span>stream&#32;documentInfo&#32;endpoints</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `stream` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.stream">Stream</a></code> |  |
| `documentInfo` | <code><a href="t-schema-http-openapiinfo.md">OpenApiInfo</a></code> |  |
| `endpoints` | <code><span><a href="t-schema-http-endpointspec.md">EndpointSpec</a>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>unit</code> |  |

## Remarks


 Request and response schemas are embedded verbatim from the generated JSON Schema text, so the published
 contract cannot drift from what the parser accepts.
