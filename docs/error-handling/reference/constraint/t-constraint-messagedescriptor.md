---
title: "Constraint.MessageDescriptor"
linkTitle: "MessageDescriptor"
weight: 1802
---

A localizable message, addressed by key rather than rendered as English.

## Signature

<div class="fsdocs-usage">
<code>type MessageDescriptor</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Key` | The stable catalogue key, for example <code>constraint.cardinality.minimum</code>. |
| `Arguments` | The operands the message interpolates, named for the key's template. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L4-4)
