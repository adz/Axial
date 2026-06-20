---
title: "Result.Check.both"
linkTitle: "both"
weight: 2301
---

Returns success when both checks succeed.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.both&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'left&gt;</span></code> | The first check. |
| `right` | <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'right&gt;</span></code> | The second check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first failure. |
