---
title: "ErrorHandling.Check.Seq.empty"
linkTitle: "empty"
weight: 2500
---

Requires an already parsed sequence-shaped value to contain no items. Null fails with an unknown actual count.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.Seq.empty&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="../result/errors/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
