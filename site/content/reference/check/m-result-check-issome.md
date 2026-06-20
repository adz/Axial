---
title: "Result.Check.isSome"
linkTitle: "isSome"
weight: 2402
type: docs
---

Returns success when the option is <code>Some</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.isSome&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for <code>Some</code>; otherwise <code>Error ()</code>. |
