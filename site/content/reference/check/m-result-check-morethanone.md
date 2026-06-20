---
title: "Result.Check.moreThanOne"
linkTitle: "moreThanOne"
weight: 2707
type: docs
---

Returns success when the sequence contains more than one item.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.moreThanOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="/reference/Axial/axial-result-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok ()</code> when more than one item is present; otherwise a cardinality failure. |
