---
title: "Result.Check.whenSome"
linkTitle: "whenSome"
weight: 2902
type: docs
---

Keeps the option when it is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;<span>'value&#32;option</span>&gt;</span></code> | <code>Ok option</code> for <code>Some</code>; otherwise <code>Error ()</code>. |
