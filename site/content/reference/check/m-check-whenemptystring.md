---
title: "Check.whenEmptyString"
linkTitle: "whenEmptyString"
weight: 2917
type: docs
---

Keeps the string when it is exactly empty, not null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenEmptyString&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> for <code>&quot;&quot;</code>; otherwise <code>Error ()</code>. |
