---
title: "Result.Check.takeAtMostOne"
linkTitle: "takeAtMostOne"
weight: 3007
type: docs
---

Takes zero or one item from a sequence when it contains at most one item.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.takeAtMostOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'value&#32;option</span>,&#32;<a href="/reference/Axial/axial-result-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok None</code> for empty, <code>Ok (Some value)</code> for one item, or a cardinality failure. |
