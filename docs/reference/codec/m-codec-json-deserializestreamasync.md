---
title: "Codec.Json.deserializeStreamAsync"
linkTitle: "deserializeStreamAsync"
weight: 2106
---

Reads a stream to end into a pooled buffer, then deserializes it as UTF-8 JSON through a compiled codec.

## Signature

<div class="fsdocs-usage">
<code><span>Codec.Json.deserializeStreamAsync&#32;<span>codec&#32;stream</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `codec` | <code><span><a href="t-codec-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> |  |
| `stream` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.stream">Stream</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;'model&gt;</span></code> |  |

## Remarks


 This reads the whole stream before decoding; there is no incremental/streaming JSON parser pre-1.0. Not available
 on Fable.
