---
title: "Schema.Http.ProblemDetails.ofParsed"
linkTitle: "ofParsed"
weight: 2102
type: docs
---

Builds a 400 problem-details value from a failed parse, or <code>None</code> when parsing succeeded.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.ProblemDetails.ofParsed&#32;<span>parsed</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `parsed` | <code><span><a href="../interpreters/t-schema-parsedinput.md">ParsedInput</a>&lt;<span>'model,&#32;<a href="../interpreters/t-schema-schemaerror.md">SchemaError</a></span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-http-problemdetails.md">ProblemDetails</a>&#32;option</span></code> |  |
