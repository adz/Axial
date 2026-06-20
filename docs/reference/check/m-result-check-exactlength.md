---
title: "Result.Check.exactLength"
linkTitle: "exactLength"
weight: 2608
---

Returns success when the string length equals the supplied length.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.exactLength&#32;<span>expected&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> | The expected length. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="/reference/Axial/axial-result-stringlengthfailure.html">StringLengthFailure</a></span>&gt;</span></code> | <code>Ok ()</code> when the length matches; otherwise a length failure. |
