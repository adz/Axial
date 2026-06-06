---
title: "Check.whenNullOrEmpty"
linkTitle: "whenNullOrEmpty"
weight: 2915
---

Keeps the string when it is null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenNullOrEmpty&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> for null or empty strings; otherwise <code>Error ()</code>. |
