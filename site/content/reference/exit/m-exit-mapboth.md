---
title: "Exit.mapBoth"
linkTitle: "mapBoth"
weight: 2103
type: docs
---

Transforms both success and failure outcomes of an exit using the provided functions.

## Signature

<div class="fsdocs-usage">
<code><span>Exit.mapBoth&#32;<span>onSuccess&#32;onFailure&#32;exit</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `onSuccess` | <code><span>'v&#32;->&#32;'w</span></code> | The function to transform the success value. |
| `onFailure` | <code><span><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cause-1.html">Cause</a>&lt;'e&gt;</span>&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cause-1.html">Cause</a>&lt;'f&gt;</span></span></code> | The function to transform the failure cause. |
| `exit` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | The exit outcome to transform. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'w,&#32;'f</span>&gt;</span></code> | A new exit outcome with transformed values. |
