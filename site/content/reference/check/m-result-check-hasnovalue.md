---
title: "Result.Check.hasNoValue"
linkTitle: "hasNoValue"
weight: 2501
type: docs
---

Returns success when the nullable has no value.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.hasNoValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when <code>HasValue</code> is false; otherwise <code>Error ()</code>. |
