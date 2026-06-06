---
title: "Check.takeSingle"
linkTitle: "takeSingle"
weight: 3006
---

Takes the only item from a sequence when it contains exactly one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.takeSingle&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="t-cardinalityfailure.md">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok value</code> when exactly one item is present; otherwise a cardinality failure. |
