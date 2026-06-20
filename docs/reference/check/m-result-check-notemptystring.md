---
title: "Result.Check.notEmptyString"
linkTitle: "notEmptyString"
weight: 2602
---

Returns success when the string has length greater than zero.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.notEmptyString&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for non-null strings with length greater than zero; otherwise <code>Error ()</code>. |
