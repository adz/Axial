---
title: "Check.whenMoreThanOne"
linkTitle: "whenMoreThanOne"
weight: 2933
---

Keeps the collection when it contains more than one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenMoreThanOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'collection,&#32;<a href="t-cardinalityfailure.md">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok values</code> when more than one item is present; otherwise a cardinality failure. |
