---
title: "Take.whenAtMostOne"
linkTitle: "whenAtMostOne"
weight: 2105
type: docs
---

Keeps the collection when it contains at most one item.

## Signature

<div class="fsdocs-usage">
<code><span>Take.whenAtMostOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'collection,&#32;<a href="../check/t-cardinalityfailure.md">CardinalityFailure</a></span>&gt;</span></code> | <code>Ok values</code> when zero or one item is present; otherwise a cardinality failure. |

## Remarks


 The check enumerates up to two items. If <span class="fsdocs-param-name">values</span> is a lazy sequence, the
 sequence may be enumerated once for the check and again by later code that consumes the
 returned value. Prefer reusable collections such as arrays or lists for this preserving form.
