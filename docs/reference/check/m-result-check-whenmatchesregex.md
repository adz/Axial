---
title: "Result.Check.whenMatchesRegex"
linkTitle: "whenMatchesRegex"
weight: 2923
---

Keeps the string when it matches the supplied regular expression pattern.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenMatchesRegex&#32;<span>pattern&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | <code>string</code> | The regular expression pattern. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;string&gt;</span></code> | <code>Ok value</code> when the string matches; otherwise <code>Error ()</code>. |
