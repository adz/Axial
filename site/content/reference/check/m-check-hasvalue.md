---
title: "Check.hasValue"
linkTitle: "hasValue"
weight: 2500
type: docs
---

Returns success when the nullable has a value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.hasValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when <code>HasValue</code> is true; otherwise <code>Error ()</code>. |
