---
title: "Constraint.Membership"
linkTitle: "Membership"
weight: 1104
type: docs
---

What a membership rule expects.

## Signature

<div class="fsdocs-usage">
<code>type Membership</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `OneOf` | The value equals one of the supplied choices. |
| `NoneOf` | The value equals none of the supplied choices. |
| `Contains` | The collection contains the supplied item. |
| `NotContains` | The collection does not contain the supplied item. |

## Remarks


 The excluding cases are primitives in their own right, not a general complement operator. There is no honest
 general <code>not</code> — see <code>Constraint.notWith</code> — but a closed membership family can state exclusion
 directly, and does, for the same reason <code>RelationOperator.NotEqual</code> states inequality directly rather
 than negating <code>Equal</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L59-59)
