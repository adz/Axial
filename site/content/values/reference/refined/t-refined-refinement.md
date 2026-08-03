---
title: "Refined.Refinement"
linkTitle: "Refinement<underlying, refined>"
weight: 1600
type: docs
---

Admission into an invariant-carrying value, and its total reverse projection.

## Signature

<div class="fsdocs-usage">
<code>type Refinement<'underlying, 'refined></code>
</div>

## Type Parameters

| Name |
| --- |
| `underlying` |
| `refined` |

## Remarks


 A refinement stores exactly one <a href="../constraint/t-constraint-constraint.md">Constraint</a> over the raw representation,
 the constructor that stamps the invariant into the type, and the projection back to the raw value. The stored
 constraint is the same value a caller can check, inspect, or attach to a schema directly: the raw-to-refined
 projection is a known representation boundary, not an opaque one, so Schema lowers the constraint unchanged
 in raw-schema context.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refinement.fs#L14-14)
