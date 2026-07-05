---
title: "ErrorHandling.Result.withError"
linkTitle: "withError"
weight: 2204
type: docs
---

Replaces a unit error with the supplied typed error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.withError&#32;<span>failure&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;unit</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |
