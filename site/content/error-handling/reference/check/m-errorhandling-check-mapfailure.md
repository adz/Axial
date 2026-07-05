---
title: "ErrorHandling.Check.mapFailure"
linkTitle: "mapFailure"
weight: 2103
type: docs
---

Maps every failure produced by a check.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.mapFailure&#32;<span>mapper&#32;check&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span><a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;->&#32;<a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a></span></code> |  |
| `check` | <code><span><a href="t-errorhandling-check.md">Check</a>&lt;'value&gt;</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
