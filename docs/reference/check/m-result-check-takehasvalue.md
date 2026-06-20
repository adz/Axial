---
title: "Result.Check.takeHasValue"
linkTitle: "takeHasValue"
weight: 3002
---

Takes the value from a nullable when it has a value.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.takeHasValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> when <code>HasValue</code> is true; otherwise <code>Error ()</code>. |
