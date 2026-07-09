---
title: "ErrorHandling.Result.someOr"
linkTitle: "someOr"
weight: 2300
---

Takes the value from an option when it is <code>Some</code>, or returns the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.someOr&#32;<span>failure&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `value` | <code><span>'value&#32;option</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |
