---
title: "Flow.HttpClient.DSL.fetchJson"
linkTitle: "fetchJson"
weight: 2622
---

 Sends the request and decodes the JSON response.
 <example><code>GET $"{root}/users/{id}" |&gt; fetchJson (Json.deserializeResult codec)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.fetchJson&#32;<span>decode&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `decode` | <code><span>string&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;string</span>&gt;</span></span></code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'b,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;'a</span>&gt;</span></code> |  |
