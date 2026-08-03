---
title: "Constraint.MessageDescriptor"
linkTitle: "MessageDescriptor"
weight: 1802
---

A message identity and the operands its template may interpolate.

## Signature

<div class="fsdocs-usage">
<code>type MessageDescriptor</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Segments` |  |
| `Values` |  |

## Remarks

<p class='fsdocs-para'>
 The identity is a parsed relative key such as <code>constraint.cardinality.between</code> or an application&#39;s own
 <code>books.isbn.invalid</code>. A descriptor never carries a document context, an attribute, an encoded resource key,
 or a plural category: those are rendering-edge facts, and a violation that captured them would stop being
 path-free comparable data.
 </p><p class='fsdocs-para'>
 The representation is private and validated, so rendering has no malformed-descriptor branch. Independently
 constructed descriptors with the same key and arguments compare equal, as do violations containing them.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L71-71)
