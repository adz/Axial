---
title: "Check.whenMinLength"
linkTitle: "whenMinLength"
weight: 2920
---

Keeps the string when its length is at least the supplied minimum.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenMinLength&#32;<span>minimum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> | The minimum accepted length. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>string,&#32;<a href="t-stringlengthfailure.md">StringLengthFailure</a></span>&gt;</span></code> | <code>Ok value</code> when the length is high enough; otherwise a length failure. |
