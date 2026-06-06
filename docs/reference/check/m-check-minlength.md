---
title: "Check.minLength"
linkTitle: "minLength"
weight: 2606
---

Returns success when the string length is at least the supplied minimum.

## Signature

<div class="fsdocs-usage">
<code><span>Check.minLength&#32;<span>minimum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> | The minimum accepted length. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="t-stringlengthfailure.md">StringLengthFailure</a></span>&gt;</span></code> | <code>Ok ()</code> when the length is high enough; otherwise a length failure. |
