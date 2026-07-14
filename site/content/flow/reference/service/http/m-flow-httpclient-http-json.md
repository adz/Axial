---
title: "Flow.HttpClient.Http.json"
linkTitle: "json"
weight: 2510
type: docs
---

 Sends a request and decodes the JSON response body with the supplied decoder.
 <example><code>Http.get url |&gt; Request.acceptJson |&gt; Http.json (Json.deserializeResult codec)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Http.json&#32;<span>decode&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `decode` | <code><span>string&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;string</span>&gt;</span></span></code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;'value</span>&gt;</span></code> |  |
