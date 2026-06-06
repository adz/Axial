---
title: "Check.notEqualTo"
linkTitle: "notEqualTo"
weight: 2801
type: docs
---

Returns success when the actual value does not equal the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.notEqualTo&#32;<span>expected&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The value that should not match. |
| `actual` | <code>'value</code> | The actual value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when values differ; otherwise <code>Error ()</code>. |
