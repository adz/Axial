---
title: "Result.Check.whenNone"
linkTitle: "whenNone"
weight: 2903
---

Keeps the option when it is <code>None</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNone&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;<span>'value&#32;option</span>&gt;</span></code> | <code>Ok None</code> for <code>None</code>; otherwise <code>Error ()</code>. |
