---
title: "Constraint.Format"
linkTitle: "Format"
weight: 1105
---

The built-in text formats. Every case names one Axial-owned executable predicate.

## Signature

<div class="fsdocs-usage">
<code>type Format</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Email` | Axial&#39;s pragmatic email shape, <code>^[^@]+@[^@]+$</code>. |
| `Trimmed` | No leading or trailing whitespace. |
| `Numeric` | One or more ASCII digits. |
| `Alphanumeric` | One or more letters or digits. |
| `Pattern` | A match for the supplied .NET regular expression. |

## Remarks


 A format never carries an author-supplied name: a name supplies no semantics a predicate can be generated from,
 so it would either be unreachable or an annotation claiming interpreted logic. Open documentation formats are
 <code>SchemaFormat</code>, and arbitrary predicates are <code>Constraint.custom</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L75-75)
