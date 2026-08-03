---
title: "Constraint.MessageLeaf"
linkTitle: "MessageLeaf"
weight: 1801
type: docs
---

One leaf of a projected message tree.

## Signature

<div class="fsdocs-usage">
<code>type MessageLeaf</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Localized` | A library failure addressed by catalogue key. |
| `Verbatim` | Author-supplied prose, passed through unchanged. |

## Remarks


 Author-supplied prose on an opaque constraint passes through verbatim and is never localizable; inventing a
 resource key for user text would promise a lookup that cannot exist.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Violation.fs#L61-61)
