---
title: "Result.Check.whenAtLeastOne"
linkTitle: "whenAtLeastOne"
weight: 2932
---

Keeps the collection when it contains at least one item.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenAtLeastOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'collection,&#32;<a href="/reference/Axial/axial-result-cardinalityfailure.html">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok values</code> when one or more items are present; otherwise a cardinality failure. |
