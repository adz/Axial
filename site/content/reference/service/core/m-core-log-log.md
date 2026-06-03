---
title: "Core.Log.log"
linkTitle: "log"
weight: 2300
type: docs
---

Writes a log message at the requested level through an explicit logging service.

## Signature

<div class="fsdocs-usage">
<code><span>Core.Log.log&#32;<span>level&#32;message</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `level` | <code><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-loglevel.html">LogLevel</a></code> |  |
| `message` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> |  |
