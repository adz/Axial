---
title: "Constraint.Number"
linkTitle: "Number"
weight: 1106
---

What a numeric-property rule expects.

## Signature

<div class="fsdocs-usage">
<code>type Number</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `MultipleOf` | The value is an exact multiple of the supplied divisor under the value type's own arithmetic. |
| `Finite` | The value is neither infinite nor <code>NaN</code>. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L88-88)
