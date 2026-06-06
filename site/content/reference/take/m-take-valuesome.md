---
title: "Take.valueSome"
linkTitle: "valueSome"
weight: 2003
type: docs
---

Takes the value from a value option when it is <code>ValueSome</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Take.valueSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;voption</span></code> | The value option to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> for <code>ValueSome value</code>; otherwise <code>Error ()</code>. |
