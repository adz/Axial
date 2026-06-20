---
title: "Result.Check.whenFalse"
linkTitle: "whenFalse"
weight: 2901
---

Keeps the boolean when it is false.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenFalse&#32;<span>condition</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `condition` | <code>bool</code> | The boolean condition to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;bool&gt;</span></code> | <code>Ok false</code> when false; otherwise <code>Error ()</code>. |
