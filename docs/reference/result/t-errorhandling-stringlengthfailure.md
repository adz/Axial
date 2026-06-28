---
title: "ErrorHandling.StringLengthFailure"
linkTitle: "StringLengthFailure"
weight: 1001
---

Structured errors returned by string length helpers.

## Signature

<div class="fsdocs-usage">
<code>type StringLengthFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `ExpectedMinLength` | The string was shorter than the minimum length. |
| `ExpectedMaxLength` | The string was longer than the maximum length. |
| `ExpectedExactLength` | The string length did not match the expected length. |
