---
title: "Check.atLeastOne"
linkTitle: "atLeastOne"
weight: 2422
---

Returns success when the sequence contains at least one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.atLeastOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when one or more items are present; otherwise <code>Error ()</code>. |
