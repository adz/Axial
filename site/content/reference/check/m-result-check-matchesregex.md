---
title: "Result.Check.matchesRegex"
linkTitle: "matchesRegex"
weight: 2609
type: docs
---

Returns success when the string matches the supplied regular expression pattern.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.matchesRegex&#32;<span>pattern&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | <code>string</code> | The regular expression pattern. |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when the string matches; otherwise <code>Error ()</code>. |
