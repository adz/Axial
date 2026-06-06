---
title: "Check.whenValueSome"
linkTitle: "whenValueSome"
weight: 2904
type: docs
---

Keeps the value option when it is <code>ValueSome</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenValueSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;voption</span></code> | The value option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;<span>'value&#32;voption</span>&gt;</span></code> | <code>Ok valueOption</code> for <code>ValueSome</code>; otherwise <code>Error ()</code>. |
