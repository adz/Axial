---
title: "ErrorHandling.CheckFailure"
linkTitle: "CheckFailure"
weight: 1001
type: docs
---

Describes why an executable value check failed, without attaching source paths or raw input.

## Signature

<div class="fsdocs-usage">
<code>type CheckFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Missing` | A required value was missing. |
| `Blank` | A required text value was blank. |
| `InvalidFormat` | The value did not match the expected format. |
| `Length` | The value length did not match the expected length constraint. |
| `Range` | The value did not match the expected ordered range constraint. |
| `Count` | The sequence count did not match the expected count constraint. |
| `Equality` | The value did not match the expected equality constraint. |
| `CustomCode` | A custom value check identified by an application-defined code failed. |
