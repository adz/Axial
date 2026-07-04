---
title: "ErrorHandling.CheckCountExpectation"
linkTitle: "CheckCountExpectation"
weight: 1004
type: docs
---

Describes the count requirement that a value check expected a sequence-shaped value to satisfy against a
 caller-supplied count. Non-emptiness is not modeled here since it carries no count to report; it is its own
 top-level <a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a> case instead.

## Signature

<div class="fsdocs-usage">
<code>type CheckCountExpectation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `MinimumCount` | The sequence was expected to contain at least the supplied count. |
| `MaximumCount` | The sequence was expected to contain at most the supplied count. |
| `ExactCount` | The sequence was expected to contain exactly the supplied count. |
| `CountBetween` | The sequence was expected to contain a count inside the inclusive bounds. |
