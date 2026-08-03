---
title: "Schema.Supply"
linkTitle: "Supply"
weight: 1116
---

Whether boundary input for a field must be supplied.

## Signature

<div class="fsdocs-usage">
<code>type Supply</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Supplied` | Boundary input must be supplied. |
| `Omittable` | Boundary input may be omitted. |

## Remarks


 Supply is evaluated before a typed value exists, so it is not a value constraint and has no place in the
 <code>Constraint</code> vocabulary. It stays Schema-owned and is declared with <code>Schema.mustSupply</code> and
 <code>Schema.mayOmit</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Constraints.fs#L12-12)
