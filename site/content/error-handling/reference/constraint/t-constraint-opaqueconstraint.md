---
title: "Constraint.OpaqueConstraint"
linkTitle: "OpaqueConstraint"
weight: 1006
type: docs
---

Why a constraint is invisible to export and proof.

## Signature

<div class="fsdocs-usage">
<code>type OpaqueConstraint</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `CustomPredicate` | An arbitrary user predicate, reported with the supplied prose. |
| `RuntimeNegation` | A negation of an inner constraint. The inner tree is descriptive only. |
| `RuntimeProjection` | An arbitrary user projection applied before the inner constraint. |
| `UnsupportedOperand` | A built-in operation whose operand has no portable representation. |

## Remarks


 Each case carries exactly what that kind of opacity needs, so a custom predicate cannot claim an inner tree and
 a projection cannot lack one. Diagnostic prose lives here; the separate documentary <code>Description</code> field on
 a description node belongs to <code>Constraint.describe</code> and never affects a violation.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintDescription.fs#L10-10)
