---
title: "Flow.HttpClient.Request.jsonBodyWith"
linkTitle: "jsonBodyWith"
weight: 2313
---

 Encodes a value with the supplied serializer and sends it as JSON.
 <example><code>request |&gt; Request.jsonBodyWith (Json.serialize codec) user</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.jsonBodyWith&#32;<span>encode&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `encode` | <code><span>'value&#32;->&#32;string</span></code> |  |
| `value` | <code>'value</code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
