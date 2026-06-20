---
title: "Result.Check.any"
linkTitle: "any"
weight: 2304
---

Returns success when at least one check in the sequence succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.any&#32;<span>checks</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span>&#32;seq</span></code> | The checks to evaluate. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first success. |
