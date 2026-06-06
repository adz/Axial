---
title: "Check.notBlank"
linkTitle: "notBlank"
weight: 2604
type: docs
---

Returns success when the string is not blank.

## Signature

<div class="fsdocs-usage">
<code><span>Check.notBlank&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for non-blank strings; otherwise <code>Error ()</code>. |
