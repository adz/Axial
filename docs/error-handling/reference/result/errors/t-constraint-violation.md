---
title: "Constraint.Violation"
linkTitle: "Violation"
weight: 1000
---

Why a value failed its constraint.

## Signature

<div class="fsdocs-usage">
<code>type Violation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Atomic` | One indivisible failure. |
| `All` | Every listed failure occurred; the value failed several conjoined rules. |
| `Any` | No alternative succeeded; each listed failure is one rejected branch. |

## Remarks

<p class='fsdocs-para'>
 A diagnostic contract, not an application error union. Domain code maps a whole violation once with
 <code>Result.mapError</code>; Schema adds the path at which it occurred.
 </p><p class='fsdocs-para'>
 Violations are plain comparable data. No closure and no constraint description is reachable from one, so
 structural equality holds and a violation can be retained and compared long after the constraint that produced
 it went out of scope. There is no promised wire format.
 </p><p class='fsdocs-para'>
 Axial-produced groups are never empty and never unary: a single failing child is returned directly rather than
 wrapped. The <code>first * rest</code> shape encodes non-emptiness only; non-unarity is a normalization invariant.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L40-40)
