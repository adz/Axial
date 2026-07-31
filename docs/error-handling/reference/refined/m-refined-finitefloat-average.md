---
title: "Refined.FiniteFloat.average"
linkTitle: "average"
weight: 2727
---

 Returns the arithmetic mean. Computed by dividing before summing, so a list whose
 total would overflow still averages successfully.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.FiniteFloat.average&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;<a href="types/t-refined-finitefloat.md">FiniteFloat</a>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><a href="types/t-refined-finitefloat.md">FiniteFloat</a>,&#32;<span><a href="../result/errors/t-check-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Finite.fs#L163-163)
