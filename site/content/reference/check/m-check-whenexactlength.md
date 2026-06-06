---
title: "Check.whenExactLength"
linkTitle: "whenExactLength"
weight: 2922
type: docs
---

Keeps the string when its length equals the supplied length.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenExactLength&#32;<span>expected&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> | The expected length. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>string,&#32;<a href="t-stringlengthfailure.md">StringLengthFailure</a></span>&gt;</span></code> | <code>Ok value</code> when the length matches; otherwise a length failure. |
