---
title: "Schema.Http.ProblemDetails.ofDiagnosticsWith"
linkTitle: "ofDiagnosticsWith"
weight: 2105
type: docs
---

Builds a 400 problem-details value from parse diagnostics, rendering each error with <span class="fsdocs-param-name">render</span>.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.ProblemDetails.ofDiagnosticsWith&#32;<span>render&#32;diagnostics</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `render` | <code><span>'error&#32;->&#32;string</span></code> |  |
| `diagnostics` | <code><span><a href="../../../../error-handling/reference/diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-schema-http-problemdetails.md">ProblemDetails</a></code> |  |
