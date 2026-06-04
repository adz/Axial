---
title: "Cause.map"
linkTitle: "map"
weight: 2100
---

Transforms the error value of a failure cause using the provided function.

## Signature

<div class="fsdocs-usage">
<code><span>Cause.map&#32;<span>mapper&#32;cause</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'e&#32;->&#32;'f</span></code> | The function to transform the error value. |
| `cause` | <code><span><a href="t-cause.md">Cause</a>&lt;'e&gt;</span></code> | The original cause to transform. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-cause.md">Cause</a>&lt;'f&gt;</span></code> | A new cause with the transformed error value, or the original cause if it was not a <code>Fail</code>. |
