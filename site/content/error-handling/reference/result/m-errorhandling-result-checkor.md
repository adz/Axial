---
title: "ErrorHandling.Result.checkOr"
linkTitle: "checkOr"
weight: 2202
type: docs
---

Returns <code>Ok ()</code> when the condition is true, or the supplied error when it is false.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.checkOr&#32;<span>failure&#32;condition</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `condition` | <code>bool</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;'error</span>&gt;</span></code> |  |
