---
title: "Check.either"
linkTitle: "either"
weight: 2302
---

Returns success when either check succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.either&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="t-check.md">Check</a>&lt;'left&gt;</span></code> | The first check. |
| `right` | <code><span><a href="t-check.md">Check</a>&lt;'right&gt;</span></code> | The second check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first success. |
