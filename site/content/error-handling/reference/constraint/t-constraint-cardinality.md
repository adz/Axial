---
title: "Constraint.Cardinality"
linkTitle: "Cardinality"
weight: 1101
type: docs
---

What a size rule expects of a text length or collection count.

## Signature

<div class="fsdocs-usage">
<code>type Cardinality</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Exact` | Exactly the supplied size. |
| `Minimum` | At least the supplied size. |
| `Maximum` | At most the supplied size. |
| `Between` | A size inside the supplied inclusive bounds. |

## Remarks


 Shape-neutral. An interpreter combines it with the schema shape to reach <code>maxLength</code>, <code>maxItems</code>, or
 <code>maxProperties</code>. Text sizes count Unicode code points, not UTF-16 code units.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L20-20)
