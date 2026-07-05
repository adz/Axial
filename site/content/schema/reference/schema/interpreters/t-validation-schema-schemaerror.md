---
title: "Validation.Schema.SchemaError"
linkTitle: "SchemaError"
weight: 1200
type: docs
---

Schema input, model validation, and contextual rule failures attached to diagnostics paths.

## Signature

<div class="fsdocs-usage">
<code>type SchemaError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Required` | A required boundary value was missing. |
| `ExpectedScalar` | The raw input value was expected to be a scalar. |
| `ExpectedObject` | The raw input value was expected to be an object. |
| `ExpectedMany` | The raw input value was expected to be a collection. |
| `InvalidFormat` | The scalar text did not match the expected format. |
| `ParseOutOfRange` | The scalar text was outside the supported range for the target primitive type. |
| `InvalidLength` | The value length did not match the expected length constraint. |
| `OutOfRange` | The value was outside the required ordered range. |
| `InvalidCount` | The collection count was outside the required count range. |
| `NotOneOf` | The value was not one of the expected choices. |
| `Duplicate` | A duplicate value was found. |
| `ConstructorFailed` | A trusted model constructor rejected otherwise-valid field values. |
| `Custom` | A custom schema failure code, with an optional custom message. |
