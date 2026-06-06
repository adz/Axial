---
title: "Check.whenMaxLength"
linkTitle: "whenMaxLength"
weight: 2921
type: docs
---

Keeps the string when its length is at most the supplied maximum.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenMaxLength&#32;<span>maximum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>int</code> | The maximum accepted length. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>string,&#32;<a href="t-stringlengthfailure.md">StringLengthFailure</a></span>&gt;</span></code> | <code>Ok value</code> when the length is low enough; otherwise a length failure. |
