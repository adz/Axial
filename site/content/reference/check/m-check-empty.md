---
title: "Check.empty"
linkTitle: "empty"
weight: 2505
type: docs
---

Returns success when the sequence is empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.empty&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for empty sequences; otherwise <code>Error ()</code>. |
