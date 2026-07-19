---
title: "ErrorHandling.CheckFailure"
linkTitle: "CheckFailure"
weight: 1001
---

Describes why an executable value check failed, without attaching source paths or structured data.

## Signature

<div class="fsdocs-usage">
<code>type CheckFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Required` | A required value was missing. |
| `InvalidFormat` | The value did not match the expected format. |
| `InvalidLength` | The value length did not match the expected length constraint. |
| `OutOfRange` | The value did not match the expected ordered range constraint. |
| `InvalidCount` | The sequence count did not match the expected count constraint. |
| `NotOneOf` | The value was not one of the expected choices. |
| `Duplicate` | A duplicate value was found. |
| `Custom` | A custom value check identified by an application-defined code failed. |
