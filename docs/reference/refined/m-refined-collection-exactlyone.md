---
title: "Refined.Collection.exactlyOne"
linkTitle: "exactlyOne"
weight: 2405
---

Extracts the only item from a sequence.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Collection.exactlyOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></code> |  |

## Remarks


 Cardinality is a collection-level structural fact, not a value-level constraint on a single element, so this
 lives here rather than as a <code>Check</code>: <code>Check.Seq.count 1</code> proves the fact and keeps the sequence,
 while this extracts the element itself, the same distinction <a href="https://learn.microsoft.com/dotnet/api/axial.refined.refine.withcheck">Refine.withCheck</a>
 draws between proving and constructing.
