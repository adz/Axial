---
title: "ErrorHandling.Result.okOr"
linkTitle: "okOr"
weight: 2306
type: docs
---

Takes the successful value from a result, or returns the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.okOr&#32;<span>failure&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'nextError</code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'nextError</span>&gt;</span></code> |  |
