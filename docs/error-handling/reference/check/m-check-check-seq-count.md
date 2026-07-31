---
title: "Check.Seq.count"
linkTitle: "count"
weight: 2502
---

Requires an already parsed sequence-shaped value to contain exactly the supplied count. Null fails with an unknown actual count.

## Signature

<div class="fsdocs-usage">
<code><span>Check.Check.Seq.count&#32;<span>expected&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> |  |
| `values` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../result/errors/t-check-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Check/Check.fs#L350-350)
