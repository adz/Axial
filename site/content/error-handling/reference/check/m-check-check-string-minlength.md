---
title: "Check.String.minLength"
linkTitle: "minLength"
weight: 2303
type: docs
---

Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.

## Signature

<div class="fsdocs-usage">
<code><span>Check.Check.String.minLength&#32;<span>minimum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |
| `value` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>string,&#32;<span><a href="../result/errors/t-check-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Check/Check.fs#L225-225)
