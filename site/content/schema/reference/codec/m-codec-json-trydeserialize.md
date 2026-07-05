---
title: "Codec.Json.tryDeserialize"
linkTitle: "tryDeserialize"
weight: 2105
type: docs
---

Deserializes a JSON string, returning decode failures as a rendered message instead of raising.

## Signature

<div class="fsdocs-usage">
<code><span>Codec.Json.tryDeserialize&#32;<span>codec&#32;json</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `codec` | <code><span><a href="t-codec-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> |  |
| `json` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'model,&#32;string</span>&gt;</span></code> |  |
