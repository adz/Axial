---
title: "Check.nullOrEmpty"
linkTitle: "nullOrEmpty"
weight: 2601
type: docs
---

Returns success when the string is null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.nullOrEmpty&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for null or empty strings; otherwise <code>Error ()</code>. |
