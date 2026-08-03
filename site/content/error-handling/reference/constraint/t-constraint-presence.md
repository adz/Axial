---
title: "Constraint.Presence"
linkTitle: "Presence"
weight: 1100
type: docs
---

What a presence rule expects of a value&#39;s shape.

## Signature

<div class="fsdocs-usage">
<code>type Presence</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Present` | The value must be inhabited according to its shape. |
| `Blank` | The value must be uninhabited according to its shape. |

## Remarks

<code>Present</code> and <code>Blank</code> are exact complements for every supported reference and container shape. Null
 text, a null or empty list, array or map, <code>None</code>, <code>ValueNone</code>, an empty <code>Nullable</code>, and
 whitespace-only text are all blank.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L9-9)
