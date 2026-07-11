---
title: "Flow.Http.postString"
linkTitle: "postString"
weight: 2514
type: docs
---

 Sends a POST request with a text body, mirroring <c>HttpClient.PostAsync</c> with string content.
 <example><code>Http.postString "https://example.com/echo" "hello"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.postString&#32;<span>url&#32;content</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `url` | <code>string</code> |  |
| `content` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;<a href="t-flow-http-httpresponse.md">HttpResponse</a></span>&gt;</span></code> |  |
