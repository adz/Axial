---
title: "ErrorHandling.RangeFailure"
linkTitle: "RangeFailure<value>"
weight: 1002
---

Structured errors returned by comparison helpers.

## Signature

<div class="fsdocs-usage">
<code>type RangeFailure<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Union Cases

| Case | Description |
| --- | --- |
| `ExpectedGreaterThan` | The value was expected to be greater than the supplied lower bound. |
| `ExpectedLessThan` | The value was expected to be less than the supplied upper bound. |
| `ExpectedAtLeast` | The value was expected to be greater than or equal to the supplied lower bound. |
| `ExpectedAtMost` | The value was expected to be less than or equal to the supplied upper bound. |
| `ExpectedBetween` | The value was expected to be between the supplied inclusive bounds. |
