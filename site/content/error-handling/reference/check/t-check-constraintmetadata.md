---
title: "Check.ConstraintMetadata"
linkTitle: "ConstraintMetadata"
weight: 1002
type: docs
---

 The inspectable meaning of an executable value constraint.

## Signature

<div class="fsdocs-usage">
<code>type ConstraintMetadata</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Required` |  A value must be present. |
| `Optional` |  A boundary value may be omitted. |
| `MinLength` |  Text must contain at least the supplied number of characters. |
| `MaxLength` |  Text must contain at most the supplied number of characters. |
| `LengthBetween` |  Text length must lie inside the supplied inclusive bounds. |
| `Email` |  Text must use the supported email format. |
| `Trimmed` |  Text must have no leading or trailing whitespace. |
| `Pattern` |  Text must match the supplied regular expression. |
| `OneOf` |  Text must equal one of the supplied choices. |
| `EqualTo` |  A value must equal the supplied operand. |
| `NotEqualTo` |  A value must differ from the supplied operand. |
| `Between` |  A value must lie inside the supplied inclusive bounds. |
| `GreaterThan` |  A value must be greater than the supplied exclusive lower bound. |
| `LessThan` |  A value must be less than the supplied exclusive upper bound. |
| `AtLeast` |  A value must be greater than or equal to the supplied lower bound. |
| `AtMost` |  A value must be less than or equal to the supplied upper bound. |
| `Count` |  A collection must contain exactly the supplied number of items. |
| `MinCount` |  A collection must contain at least the supplied number of items. |
| `MaxCount` |  A collection must contain at most the supplied number of items. |
| `CountBetween` |  A collection count must lie inside the supplied inclusive bounds. |
| `Distinct` |  A collection must contain no duplicate items. |
| `Contains` |  A collection must contain the supplied item. |
| `MultipleOf` |  A numeric value must be an exact multiple of the supplied divisor. |
| `Custom` |  An application-defined constraint with a stable external code and inspectable operands. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Check/Constraint.fs#L22-22)
