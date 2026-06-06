---
title: "Check.whenHasNoValue"
linkTitle: "whenHasNoValue"
weight: 2907
---

Keeps the nullable when it has no value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenHasNoValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;<span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span>&gt;</span></code> | <code>Ok nullable</code> when <code>HasValue</code> is false; otherwise <code>Error ()</code>. |
