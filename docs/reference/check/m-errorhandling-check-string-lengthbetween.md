---
title: "ErrorHandling.Check.String.lengthBetween"
linkTitle: "lengthBetween"
weight: 2205
---

Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.String.lengthBetween&#32;<span>minimum&#32;maximum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |
| `maximum` | <code>int</code> |  |
| `value` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
