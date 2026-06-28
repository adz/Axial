---
title: "Refined.RefinementError"
linkTitle: "RefinementError"
weight: 1001
type: docs
---

Structural failures returned by built-in refinement constructors and the <code>refine { }</code> builder.

## Signature

<div class="fsdocs-usage">
<code>type RefinementError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `ParseFailed` | A primitive parse operation failed before refinement. |
| `InvalidFormat` | The value had an invalid format for the target refined type. |
| `OutOfRange` | The value was outside the accepted range for the target refined type. |
| `MissingValue` | The value required for the target refined type was missing. |
| `InvalidStructure` | The value had an invalid structure for the target refined type. |
