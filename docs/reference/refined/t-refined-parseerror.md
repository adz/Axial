---
title: "Refined.ParseError"
linkTitle: "ParseError"
weight: 1000
---

Primitive parse failures returned by <code>Parse</code> helpers.

## Signature

<div class="fsdocs-usage">
<code>type ParseError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `MissingValue` | The input was missing for the target primitive type. |
| `InvalidFormat` | The input did not match the expected format for the target primitive type. |
| `OutOfRange` | The input was outside the supported range for the target primitive type. |
