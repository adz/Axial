---
title: "ErrorHandling.Check.Seq.minCount"
linkTitle: "minCount"
weight: 2401
---

Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.Seq.minCount&#32;<span>minimum&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |
| `values` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
