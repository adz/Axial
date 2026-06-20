---
title: "Result.Check.whenBlank"
linkTitle: "whenBlank"
weight: 2919
type: docs
---

Keeps the string when it is blank.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenBlank&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> for null, empty, or whitespace strings; otherwise <code>Error ()</code>. |
