---
title: "EffectFlow.fold"
linkTitle: "fold"
weight: 2107
---



## Signature

<div class="fsdocs-usage">
<code><span>EffectFlow.fold&#32;<span>onSuccess&#32;onFailure&#32;effect</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `onSuccess` | <code><span>'value&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'next,&#32;'nextError</span>&gt;</span></span></code> |  |
| `onFailure` | <code><span><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-cause-1.html">Cause</a>&lt;'error&gt;</span>&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'next,&#32;'nextError</span>&gt;</span></span></code> |  |
| `effect` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'next,&#32;'nextError</span>&gt;</span></code> |  |
