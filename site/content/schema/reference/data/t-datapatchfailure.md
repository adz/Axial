---
title: "DataPatchFailure"
linkTitle: "DataPatchFailure"
weight: 1301
type: docs
---

Describes why one immutable data edit could not be applied.

## Signature

<div class="fsdocs-usage">
<code>type DataPatchFailure</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `EditIndex` | The zero-based position of the failing edit. |
| `Path` | The rendered path targeted by the edit. |
| `Message` | A concise explanation of the incompatible path or shape. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L73-73)
