---
title: "Constraint.AtomicViolation"
linkTitle: "AtomicViolation"
weight: 1002
type: docs
---

Why one indivisible constraint failed.

## Signature

<div class="fsdocs-usage">
<code>type AtomicViolation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Expected` | An interpreted expectation was not met. |
| `Described` | An opaque constraint failed, reported with its author-supplied prose. |
| `UnsupportedOperand` | A built-in rule failed whose operand has no portable representation. |

## Remarks

<p class='fsdocs-para'>
 An interpreted constructor reports <code>Expected</code> carrying the very same <a href="t-constraint-constraintatom.md">ConstraintAtom</a>
 its description carries, so a consumer recovers the failing constraint&#39;s identity from the failure itself. No
 code string is ever parsed to recover meaning.
 </p><p class='fsdocs-para'><code>actual = None</code> means no actual value is available in portable form — either the value&#39;s type is outside
 the portable set, or the rule could not compute one, as for the length of a null string. A portably-null actual
 is <code>Some ConstraintValue.Null</code> and is distinct from both.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L16-16)
