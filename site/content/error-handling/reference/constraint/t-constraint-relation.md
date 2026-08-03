---
title: "Constraint.Relation"
linkTitle: "Relation"
weight: 1103
type: docs
---

What an ordering or equality rule expects.

## Signature

<div class="fsdocs-usage">
<code>type Relation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Compared` | The value compares to the operand under the supplied operator. |
| `Within` | The value lies inside the supplied inclusive bounds. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L46-46)
