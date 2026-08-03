---
title: "Constraint.RelationOperator"
linkTitle: "RelationOperator"
weight: 1102
type: docs
---

The comparison a relation asserts between a value and an operand.

## Signature

<div class="fsdocs-usage">
<code>type RelationOperator</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Equal` | Values must be equal. |
| `NotEqual` | Values must differ. |
| `GreaterThan` | The value must be strictly greater than the operand. |
| `LessThan` | The value must be strictly less than the operand. |
| `AtLeast` | The value must be greater than or equal to the operand. |
| `AtMost` | The value must be less than or equal to the operand. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L31-31)
