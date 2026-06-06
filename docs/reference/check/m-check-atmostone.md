---
title: "Check.atMostOne"
linkTitle: "atMostOne"
weight: 2705
---

Returns success when the sequence contains at most one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.atMostOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<a href="t-cardinalityfailure.md">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok ()</code> when zero or one item is present; otherwise a cardinality failure. |
