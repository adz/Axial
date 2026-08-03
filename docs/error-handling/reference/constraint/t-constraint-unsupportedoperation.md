---
title: "Constraint.UnsupportedOperation"
linkTitle: "UnsupportedOperation"
weight: 1107
---

A built-in operation that received an operand outside the portable value set.

## Signature

<div class="fsdocs-usage">
<code>type UnsupportedOperation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Relation` | An ordering or equality comparison. |
| `Within` | An inclusive range. |
| `Contains` | A collection containment test. |
| `MultipleOf` | A divisibility test. |

## Remarks


 The constraint still executes against its typed closure. Description, diagnostics, and export report the
 operation honestly instead of approximating the operand. Message keys compose from the case and its operator,
 for example <code>constraint.unsupportedOperand.relation.atLeast</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L101-101)
