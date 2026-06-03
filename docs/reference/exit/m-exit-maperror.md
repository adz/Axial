---
title: "Exit.mapError"
linkTitle: "mapError"
weight: 2102
---

Transforms the error value of a failed exit outcome using the provided function.

## Signature

<div class="fsdocs-usage">
<code><span>Exit.mapError&#32;<span>mapper&#32;exit</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapper` | <code><span>'e&#32;->&#32;'f</span></code> | The function to transform the error value. |
| `exit` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | The exit outcome to transform. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'v,&#32;'f</span>&gt;</span></code> | A new exit outcome with the transformed error value. |
