---
title: "Flow.HttpClient.Http.getString"
linkTitle: "getString"
weight: 2511
---

 Sends a GET request and returns the response body, mirroring <c>HttpClient.GetStringAsync</c>.
 <example><code>Http.getString "https://example.com"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Http.getString&#32;<span>url</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `url` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;string</span>&gt;</span></code> |  |
