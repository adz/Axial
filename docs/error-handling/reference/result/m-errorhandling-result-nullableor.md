---
title: "ErrorHandling.Result.nullableOr"
linkTitle: "nullableOr"
weight: 2304
---

Takes the value from a nullable when it has a value, or returns the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.nullableOr&#32;<span>failure&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |
