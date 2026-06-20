---
title: "Result.Check.atLeastOne"
linkTitle: "atLeastOne"
weight: 2706
type: docs
---

Returns success when the sequence contains at least one item.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.atLeastOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="/reference/Axial/axial-result-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok ()</code> when one or more items are present; otherwise a cardinality failure. |
