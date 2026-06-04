---
title: "Exit.map"
linkTitle: "map"
weight: 2100
type: docs
---

Transforms the success value of an exit outcome using the provided function.

## Signature

<div class="fsdocs-usage">
<code><span>Exit.map&#32;<span>mapper&#32;exit</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'v&#32;->&#32;'w</span></code> | The function to transform the success value. |
| `exit` | <code><span><a href="t-exit.md">Exit</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | The exit outcome to transform. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-exit.md">Exit</a>&lt;<span>'w,&#32;'e</span>&gt;</span></code> | A new exit outcome with the transformed success value. |
