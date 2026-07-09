---
title: "ErrorHandling.Check.Seq.countBetween"
linkTitle: "countBetween"
weight: 2505
type: docs
---

Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.Seq.countBetween&#32;<span>minimum&#32;maximum&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |
| `maximum` | <code>int</code> |  |
| `values` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
