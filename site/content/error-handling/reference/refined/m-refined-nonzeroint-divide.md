---
title: "Refined.NonZeroInt.divide"
linkTitle: "divide"
weight: 2810
type: docs
---


 Divides by a divisor that cannot be zero, so division by zero is unreachable.
 Still reports overflow, which occurs only for <code>Int32.MinValue / -1</code>.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonZeroInt.divide&#32;<span>dividend&#32;divisor</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `dividend` | <code>int</code> |  |
| `divisor` | <code><a href="types/t-refined-nonzeroint.md">NonZeroInt</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>int,&#32;<span><a href="../result/errors/t-check-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Numeric.fs#L322-322)
