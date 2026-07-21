---
title: "Flow.HttpClient.Request.bytesBody"
linkTitle: "bytesBody"
weight: 2314
---

 Sends raw bytes with an explicit content type.
 <example><code>request |&gt; Request.bytesBody "application/octet-stream" payload</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.bytesBody&#32;<span>contentType&#32;content&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `contentType` | <code>string</code> |  |
| `content` | <code><span>byte&#32;array</span></code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
