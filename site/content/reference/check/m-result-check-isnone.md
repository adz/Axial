---
title: "Result.Check.isNone"
linkTitle: "isNone"
weight: 2403
type: docs
---

Returns success when the option is <code>None</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.isNone&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span>'value&#32;option</span></code> | The option to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for <code>None</code>; otherwise <code>Error ()</code>. |
