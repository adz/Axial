---
title: "Flow.Http.postJson"
linkTitle: "postJson"
weight: 2515
type: docs
---

 Encodes a value as JSON, POSTs it, and decodes the JSON response.
 <example><code>Http.postJson (Json.serialize codec) (Json.deserializeResult codec) url user</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.postJson&#32;<span>encode&#32;decode&#32;url&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `encode` | <code><span>'input&#32;->&#32;string</span></code> |  |
| `decode` | <code><span>string&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;string</span>&gt;</span></span></code> |  |
| `url` | <code>string</code> |  |
| `value` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;'value</span>&gt;</span></code> |  |
