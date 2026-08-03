---
title: "Constraint.ConstraintExpression"
linkTitle: "ConstraintExpression"
weight: 1004
type: docs
---

The logical form of a constraint.

## Signature

<div class="fsdocs-usage">
<code>type ConstraintExpression</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Atom` | One interpreted primitive. |
| `All` | A conjunction. The empty list is the satisfied identity. |
| `Any` | A disjunction, which always has at least one branch. |
| `Optional` | A lift over an optional container: absence passes, presence delegates to the inner constraint. |
| `Opaque` | A constraint that runs normally but cannot be exported or proved. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintDescription.fs#L21-21)
