---
title: "Check.isNone"
linkTitle: "isNone"
weight: 2403
---

Returns success when the option is <code>None</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.isNone&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for <code>None</code>; otherwise <code>Error ()</code>. |
