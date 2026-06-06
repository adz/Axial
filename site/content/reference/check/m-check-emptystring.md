---
title: "Check.emptyString"
linkTitle: "emptyString"
weight: 2603
type: docs
---

Returns success when the string is exactly empty, not null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.emptyString&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for <code>&quot;&quot;</code>; otherwise <code>Error ()</code>. |
