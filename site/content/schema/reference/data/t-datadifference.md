---
title: "DataDifference"
linkTitle: "DataDifference"
weight: 1500
type: docs
---

One focused difference between expected and actual structured data.

## Signature

<div class="fsdocs-usage">
<code>type DataDifference</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Path` | The location of the difference. |
| `Expected` | The expected value at the location, when present. |
| `Actual` | The actual value at the location, when present. |
| `Cause` | The difference category. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L131-131)
