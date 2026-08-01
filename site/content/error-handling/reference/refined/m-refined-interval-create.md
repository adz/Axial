---
title: "Refined.Interval.create"
linkTitle: "create"
weight: 2301
type: docs
---


 Builds an interval from a pair the caller asserts is already ordered, failing when
 it is not. Use this at a boundary, where an inverted pair is a caller error worth
 reporting rather than silently repairing; use <code>between</code> when either order is
 acceptable input.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.Interval.create&#32;<span>lower&#32;upper</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `lower` | <code>'value</code> |  |
| `upper` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span><a href="types/t-refined-interval.md">Interval</a>&lt;'value&gt;</span>,&#32;<span><a href="../result/errors/t-check-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Interval.fs#L71-71)
