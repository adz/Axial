---
title: "Result.Check.whenNotBlank"
linkTitle: "whenNotBlank"
weight: 2918
---

Keeps the string when it is not blank.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNotBlank&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> for non-blank strings; otherwise <code>Error ()</code>. |
