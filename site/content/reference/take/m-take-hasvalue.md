---
title: "Take.hasValue"
linkTitle: "hasValue"
weight: 2005
type: docs
---

Takes the value from a nullable when it has a value.

## Signature

<div class="fsdocs-usage">
<code><span>Take.hasValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> when <code>HasValue</code> is true; otherwise <code>Error ()</code>. |
