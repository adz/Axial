---
title: "Result.Check.notNullOrEmpty"
linkTitle: "notNullOrEmpty"
weight: 2600
---

Returns success when the string is not null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.notNullOrEmpty&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for non-empty strings; otherwise <code>Error ()</code>. |
