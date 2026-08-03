---
title: "Constraint.MessageFormatSpec"
linkTitle: "MessageFormatSpec"
weight: 1803
type: docs
---


 A descriptor plus the rendering metadata its owning catalogue holds: the neutral fallback template and the
 optional plural operand.


## Signature

<div class="fsdocs-usage">
<code>type MessageFormatSpec</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Descriptor` |  |
| `Fallback` |  |
| `Plural` |  |

## Remarks


 This separation is what lets Schema push its own <code>schema.*</code> entries through the same renderer mechanics —
 contextual fallback, <code>.one</code>/<code>.other</code> selection, interpolation, value formatting — without
 <code>Axial.Constraint</code> learning a single Schema identity, and without a reverse package dependency.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L99-99)
