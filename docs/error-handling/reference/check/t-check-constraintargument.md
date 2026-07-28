---
title: "Check.ConstraintArgument"
linkTitle: "ConstraintArgument"
weight: 1003
---

 A closed value used when constraint metadata must cross a serialization boundary.

## Signature

<div class="fsdocs-usage">
<code>type ConstraintArgument</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Text` |  Text that can cross a metadata serialization boundary. |
| `Integer` |  An integral value represented as a signed 64-bit integer. |
| `Decimal` |  A numeric value represented as a decimal. |
| `Boolean` |  A Boolean value. |
| `List` |  An ordered collection of portable arguments. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Check/Constraint.fs#L10-10)
