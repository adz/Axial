---
title: "ErrorHandling.CheckRangeExpectation"
linkTitle: "CheckRangeExpectation"
weight: 1003
---

Describes the ordering requirement that a value check expected a comparable value to satisfy against a
 caller-supplied bound. Zero-relative requirements (positive, negative, and their non-strict variants) are not
 modeled here since they carry no bound to report; they are their own top-level
 <a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a> cases instead.

## Signature

<div class="fsdocs-usage">
<code>type CheckRangeExpectation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `GreaterThan` | The value was expected to be greater than the supplied exclusive lower bound. |
| `LessThan` | The value was expected to be less than the supplied exclusive upper bound. |
| `AtLeast` | The value was expected to be greater than or equal to the supplied lower bound. |
| `AtMost` | The value was expected to be less than or equal to the supplied upper bound. |
| `Between` | The value was expected to be between the supplied inclusive bounds. |
