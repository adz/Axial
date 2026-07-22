---
title: "ErrorHandling.Check.Seq.maxCount"
linkTitle: "maxCount"
weight: 2504
---

Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.Seq.maxCount&#32;<span>maximum&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>int</code> |  |
| `values` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="../result/errors/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
