---
title: "Schema.Http.Endpoint.returnsJson"
linkTitle: "returnsJson"
weight: 2211
---

Adds a JSON response whose body is described by the schema's generated JSON Schema.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.Endpoint.returnsJson&#32;<span>status&#32;description&#32;schema&#32;spec</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `status` | <code>int</code> |  |
| `description` | <code>string</code> |  |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `spec` | <code><a href="t-schema-http-endpointspec.md">EndpointSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-schema-http-endpointspec.md">EndpointSpec</a></code> |  |
