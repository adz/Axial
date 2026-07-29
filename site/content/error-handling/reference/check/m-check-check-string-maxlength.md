---
title: "Check.String.maxLength"
linkTitle: "maxLength"
weight: 2304
type: docs
---

Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.

## Signature

<div class="fsdocs-usage">
<code><span>Check.Check.String.maxLength&#32;<span>maximum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>int</code> |  |
| `value` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../result/errors/t-check-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Check/Check.fs#L199-199)
