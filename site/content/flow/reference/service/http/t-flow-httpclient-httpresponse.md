---
title: "Flow.HttpClient.HttpResponse"
linkTitle: "HttpResponse"
weight: 1005
type: docs
---

 The complete response transcript for one HTTP exchange.

## Signature

<div class="fsdocs-usage">
<code>type HttpResponse</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `StatusCode` |  The numeric response status code. |
| `ReasonPhrase` |  The reason phrase supplied by the server, or an empty string. |
| `Headers` |  Response and content headers, in arrival order. |
| `Body` |  The exact response body bytes. |
| `Text` |  The response body decoded with the response charset (UTF-8 when unspecified). |
| `Request` |  The redacted request line, such as <c>GET https://api.example.com/users/***</c>. |
| `StartedAt` |  When the request started. |
| `Duration` |  Total exchange duration including body download. |
