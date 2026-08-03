---
title: "Constraint.ConstraintValue"
linkTitle: "ConstraintValue"
weight: 1007
type: docs
---


 The closed set of operand and actual-value representations a constraint may carry across a description,
 diagnostic, or localization boundary.


## Signature

<div class="fsdocs-usage">
<code>type ConstraintValue</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Text` | Text. |
| `Char` | A single character. |
| `Integer` | An integral value that fits a signed 64-bit integer. |
| `BigInteger` | An arbitrary-width integer. |
| `Decimal` | An exact base-10 value. |
| `Float` | An IEEE double, retained without passing through <code>decimal</code>. |
| `Float32` | An IEEE single, retained without passing through <code>decimal</code>. |
| `Boolean` | A Boolean. |
| `Guid` | A globally unique identifier, kept distinct from its textual spelling. |
| `DateTime` | A date and time without an offset. |
| `DateTimeOffset` | A date and time with an offset from UTC. |
| `TimeSpan` | A duration. |
| `Null` | An absent reference. Distinct from "no portable representation available". |
| `List` | An ordered collection of portable values. |

## Remarks

<p class='fsdocs-para'>
 A value is admitted only when the representation is lossless in the semantics Axial&#39;s runtime diagnostics and
 exporters use. Semantic sorts keep their own case rather than being flattened into <code>Text</code> because their
 wire rendering happens to be textual: an instant and the string spelling it are different facts, and an
 interpreter that cannot tell them apart cannot decide whether wire equality substitutes for typed equality.
 </p><p class='fsdocs-para'><code>Guid</code> and <code>TimeSpan</code> are reached through typed dispatch rather than a runtime type test. Fable
 erases a <code>Guid</code> to a plain string and a <code>TimeSpan</code> to a number, so a boxed type test silently
 labels them <code>Text</code> and <code>Integer</code> there while .NET labels them correctly — the same constraint
 meaning two different things per platform. <code>ConstraintValue.ofOperand</code> resolves the overload at the
 call site, where the type is still known, so both platforms agree.
 </p><p class='fsdocs-para'>
 Values outside this set are never boxed through the public surface. The constraint still executes against its
 private typed closure; the atom describes and fails as <code>UnsupportedOperand</code> instead.
 </p><p class='fsdocs-para'>
 This is not a solver literal theory. A later proof phase declares its own numeric, date, and string sorts and
 adds a translation from the cases here; nothing may be added to this type for the solver alone.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintValue.fs#L93-93)
