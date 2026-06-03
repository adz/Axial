---
title: "Exit.bind"
linkTitle: "bind"
weight: 2101
type: docs
---

Binds the success value of an exit outcome to a function that returns a new exit outcome.

## Signature

<div class="fsdocs-usage">
<code><span>Exit.bind&#32;<span>binder&#32;exit</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'v&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'w,&#32;'e</span>&gt;</span></span></code> | The function that takes a success value and returns a new exit outcome. |
| `exit` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | The exit outcome to bind. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'w,&#32;'e</span>&gt;</span></code> | The result of the binder function if the exit was successful; otherwise, the original failure. |
