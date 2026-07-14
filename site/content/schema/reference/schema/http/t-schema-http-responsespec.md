---
title: "Schema.Http.ResponseSpec"
linkTitle: "ResponseSpec"
weight: 1201
type: docs
---

One documented response of an endpoint.

## Signature

<div class="fsdocs-usage">
<code>type ResponseSpec</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Status` | The HTTP status code. |
| `Description` | Human-readable description required by OpenAPI. |
| `JsonSchema` | JSON Schema text for the response body, or <code>None</code> for a body-less response. |
| `MediaType` | Media type of the response body; ignored when <a href="t-schema-http-responsespec.md">ResponseSpec.JsonSchema</a> is <code>None</code>. |
