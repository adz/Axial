---
title: "Constraint.MessageTree"
linkTitle: "MessageTree"
weight: 1800
---

A violation projected for an external localization system, retaining its grouping.

## Signature

<div class="fsdocs-usage">
<code>type MessageTree</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Leaf` | One message. |
| `All` | Messages for conjoined failures. |
| `Any` | Messages for rejected alternatives. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L68-68)
