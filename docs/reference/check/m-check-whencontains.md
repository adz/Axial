---
title: "Check.whenContains"
linkTitle: "whenContains"
weight: 2926
---

Keeps the collection when it contains the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenContains&#32;<span>expected&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The value to search for. |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'collection&gt;</span></code> | <code>Ok values</code> when the value is present; otherwise <code>Error ()</code>. |
