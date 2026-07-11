---
title: "Flow.Http.Request.create"
linkTitle: "create"
weight: 2300
type: docs
---

 Creates a request with the supplied method and already-formed URL.
 <example><code>Request.create Method.Get "https://api.example.com/users"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Request.create&#32;<span>method&#32;url</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `method` | <code><a href="t-flow-http-method.md">Method</a></code> |  |
| `url` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
