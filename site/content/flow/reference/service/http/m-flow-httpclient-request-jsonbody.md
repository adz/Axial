---
title: "Flow.HttpClient.Request.jsonBody"
linkTitle: "jsonBody"
weight: 2312
type: docs
---

 Sends an already-serialized JSON body with the <c>application/json</c> content type.
 <example><code>request |&gt; Request.jsonBody """{"name":"Ada"}"""</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.jsonBody&#32;<span>json&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `json` | <code>string</code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
