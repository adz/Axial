---
title: "Constraint.MessageKeyError"
linkTitle: "MessageKeyError"
weight: 1804
---

Why a relative message key could not be parsed.

## Signature

<div class="fsdocs-usage">
<code>type MessageKeyError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `EmptyKey` | The key was empty. |
| `EmptySegment` | The zero-based segment at this position was empty, as in <code>books..isbn</code>. |

## Remarks


 A key is <code>segment (&quot;.&quot; segment)*</code>. Dots separate segments and are never literal segment data; every other
 character, including <code>%</code>, brackets, whitespace, and non-ASCII text, is exact input that the resource-segment
 encoder handles later. Callers never pre-encode a key.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L15-15)
