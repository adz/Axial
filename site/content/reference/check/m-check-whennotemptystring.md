---
title: "Check.whenNotEmptyString"
linkTitle: "whenNotEmptyString"
weight: 2916
type: docs
---

Keeps the string when it has length greater than zero.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenNotEmptyString&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> for non-null strings with length greater than zero; otherwise <code>Error ()</code>. |
