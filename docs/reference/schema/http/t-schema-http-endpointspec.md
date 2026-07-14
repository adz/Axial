---
title: "Schema.Http.EndpointSpec"
linkTitle: "EndpointSpec"
weight: 1200
---

A host-neutral description of one schema-driven endpoint, used to assemble OpenAPI documents.

## Signature

<div class="fsdocs-usage">
<code>type EndpointSpec</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Method` |  |
| `Path` |  |
| `Summary` |  |
| `OperationId` |  |
| `Tags` |  |
| `RequestSchema` | JSON Schema text for the request body, produced by <a href="https://learn.microsoft.com/dotnet/api/axial.schema.jsonschemamodule.generate">JsonSchemaModule.generate</a>. |
| `Responses` |  |

## Remarks


 The spec deliberately does not describe routing or handlers: hosts keep their own idioms for those. It records
 the boundary contract — method, path, request schema, and responses — which is the part every host shares.
