---
title: "Result.Check.maxLength"
linkTitle: "maxLength"
weight: 2607
---

Returns success when the string length is at most the supplied maximum.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.maxLength&#32;<span>maximum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>int</code> | The maximum accepted length. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="/reference/Axial/axial-result-stringlengthfailure.html">StringLengthFailure</a></span>&gt;</span></code> | <code>Ok ()</code> when the length is low enough; otherwise a length failure. |
