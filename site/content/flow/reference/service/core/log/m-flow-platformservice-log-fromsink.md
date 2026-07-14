---
title: "Flow.PlatformService.Log.fromSink"
linkTitle: "fromSink"
weight: 2309
type: docs
---

Creates a logger from a synchronous sink function. Exceptions are appended to the message text.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.PlatformService.Log.fromSink&#32;<span>sink</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `sink` | <code><span><a href="../t-flow-loglevel.md">LogLevel</a>&#32;->&#32;string&#32;->&#32;unit</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../t-flow-platformservice-ilog.md">ILog</a></code> |  |
