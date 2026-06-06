---
title: "Check.blank"
linkTitle: "blank"
weight: 2605
type: docs
---

Returns success when the string is blank.

## Signature

<div class="fsdocs-usage">
<code><span>Check.blank&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for null, empty, or whitespace strings; otherwise <code>Error ()</code>. |
