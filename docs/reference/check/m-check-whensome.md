---
title: "Check.whenSome"
linkTitle: "whenSome"
weight: 2902
---

Keeps the option when it is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;<span>'value&#32;option</span>&gt;</span></code> | <code>Ok option</code> for <code>Some</code>; otherwise <code>Error ()</code>. |
