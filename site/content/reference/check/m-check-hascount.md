---
title: "Check.hasCount"
linkTitle: "hasCount"
weight: 2701
type: docs
---

Returns success when the sequence count equals the expected count.

## Signature

<div class="fsdocs-usage">
<code><span>Check.hasCount&#32;<span>expected&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> | The expected item count. |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when the count matches; otherwise <code>Error ()</code>. |
