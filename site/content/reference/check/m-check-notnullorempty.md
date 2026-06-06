---
title: "Check.notNullOrEmpty"
linkTitle: "notNullOrEmpty"
weight: 2600
type: docs
---

Returns success when the string is not null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.notNullOrEmpty&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for non-empty strings; otherwise <code>Error ()</code>. |
