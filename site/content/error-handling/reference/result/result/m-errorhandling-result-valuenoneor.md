---
title: "ErrorHandling.Result.valueNoneOr"
linkTitle: "valueNoneOr"
weight: 2303
type: docs
---

Returns success when the value option is <code>ValueNone</code>, or returns the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.valueNoneOr&#32;<span>failure&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `value` | <code><span>'value&#32;voption</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;'error</span>&gt;</span></code> |  |
