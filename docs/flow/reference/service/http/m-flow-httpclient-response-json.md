---
title: "Flow.HttpClient.Response.json"
linkTitle: "json"
weight: 2404
---

 Decodes the response body with the supplied decoder, mapping failure to <c>HttpError.DecodeFailed</c>.
 <example><code>response |&gt; Response.json (Json.deserializeResult codec)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Response.json&#32;<span>decode&#32;response</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `decode` | <code><span>string&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;string</span>&gt;</span></span></code> |  |
| `response` | <code><a href="t-flow-httpclient-httpresponse.md">HttpResponse</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a></span>&gt;</span></code> |  |
