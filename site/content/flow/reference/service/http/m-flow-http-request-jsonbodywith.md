---
title: "Flow.Http.Request.jsonBodyWith"
linkTitle: "jsonBodyWith"
weight: 2313
type: docs
---

 Encodes a value with the supplied serializer and sends it as JSON.
 <example><code>request |&gt; Request.jsonBodyWith (Json.serialize codec) user</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Request.jsonBodyWith&#32;<span>encode&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `encode` | <code><span>'value&#32;->&#32;string</span></code> |  |
| `value` | <code>'value</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
