---
title: "Refined.Bounded"
linkTitle: "Bounded<value>"
weight: 1008
---

A value paired with the inclusive interval it is known to lie within.

## Signature

<div class="fsdocs-usage">
<code>type Bounded<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Record Fields

| Field | Description |
| --- | --- |
| `BoundedValue` |  |
| `BoundsValue` |  |

## Remarks


 The bounds are carried at run time rather than in type parameters. F# has no
 type-level naturals, so a <code>Bounded&lt;&#39;value, &#39;min, &#39;max&gt;</code> would need Peano-encoded
 phantom types — unreadable inference errors, and nothing Fable can compile. Runtime
 bounds also let <code>clamp</code> and <code>normalize</code> fall out of
 <a href="t-refined-interval.md">Interval</a> instead of duplicating a second bounds concept.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Bounded.fs#L13-13)
