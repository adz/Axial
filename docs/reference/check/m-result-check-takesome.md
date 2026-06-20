---
title: "Result.Check.takeSome"
linkTitle: "takeSome"
weight: 3000
---

Takes the value from an option when it is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.takeSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> for <code>Some value</code>; otherwise <code>Error ()</code>. |
