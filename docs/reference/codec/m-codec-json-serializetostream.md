---
title: "Codec.Json.serializeToStream"
linkTitle: "serializeToStream"
weight: 2103
---

Serializes a trusted model as UTF-8 JSON directly to a stream through a compiled codec, flushing once when complete.

## Signature

<div class="fsdocs-usage">
<code><span>Codec.Json.serializeToStream&#32;<span>codec&#32;stream&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `codec` | <code><span><a href="t-codec-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> |  |
| `stream` | <code><a href="https://learn.microsoft.com/dotnet/api/system.io.stream">Stream</a></code> |  |
| `value` | <code>'model</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>unit</code> |  |

## Remarks


 Encodes into a pooled buffer and writes it to <span class="fsdocs-param-name">stream</span> in one call, so the response path never
 materializes an intermediate string. Not available on Fable.
