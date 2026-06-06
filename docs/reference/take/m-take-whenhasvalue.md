---
title: "Take.whenHasValue"
linkTitle: "whenHasValue"
weight: 2004
---

Keeps the nullable when it has a value.

## Signature

<div class="fsdocs-usage">
<code><span>Take.whenHasValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;<span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span>&gt;</span></code> | <code>Ok nullable</code> when <code>HasValue</code> is true; otherwise <code>Error ()</code>. |
