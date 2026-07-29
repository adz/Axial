---
title: "Check.CheckFailure"
linkTitle: "CheckFailure"
weight: 1000
type: docs
---

Describes why an executable value check failed, without attaching source paths or structured data.

## Signature

<div class="fsdocs-usage">
<code>type CheckFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Blank` | The value was not inhabited for its shape. |
| `InvalidFormat` | The value did not match the expected format. |
| `InvalidLength` | The value length did not match the expected length constraint. |
| `OutOfRange` | The value did not match the expected ordered range constraint. |
| `NotOneOf` | The value was not one of the expected choices. |
| `Duplicate` | A duplicate value was found. |
| `Custom` | A custom value check identified by an application-defined code failed. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Check/Check.fs#L34-34)
