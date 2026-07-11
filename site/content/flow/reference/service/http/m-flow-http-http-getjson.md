---
title: "Flow.Http.getJson"
linkTitle: "getJson"
weight: 2513
type: docs
---

 Sends a GET request and decodes the JSON response.
 <example><code>Http.getJson (Json.deserializeResult codec) "https://api.example.com/users/1"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.getJson&#32;<span>decode&#32;url</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `decode` | <code><span>string&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;string</span>&gt;</span></span></code> |  |
| `url` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;'value</span>&gt;</span></code> |  |
