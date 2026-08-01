---
title: "DataMismatch"
linkTitle: "DataMismatch"
weight: 1504
type: docs
---

One failed selective or recursive data expectation.

## Signature

<div class="fsdocs-usage">
<code>type DataMismatch</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `ExpectationIndex` | The zero-based position of the top-level expectation. |
| `Path` | The full path at which matching failed. |
| `Expected` | A concise description of the expected observation. |
| `Actual` | The actual value, or <code>None</code> when it was absent. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomicsTypes.fs#L154-154)
