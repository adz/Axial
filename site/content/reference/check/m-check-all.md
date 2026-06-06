---
title: "Check.all"
linkTitle: "all"
weight: 2303
type: docs
---

Returns success when every check in the sequence succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.all&#32;<span>checks</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="t-check.md">Check</a>&lt;'value&gt;</span>&#32;seq</span></code> | The checks to evaluate. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first failure. |
