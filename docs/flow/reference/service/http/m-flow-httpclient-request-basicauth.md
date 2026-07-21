---
title: "Flow.HttpClient.Request.basicAuth"
linkTitle: "basicAuth"
weight: 2306
---

 Adds a basic-auth Authorization header. The credentials are always redacted in diagnostics.
 <example><code>request |&gt; Request.basicAuth user password</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.basicAuth&#32;<span>user&#32;password&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `user` | <code>string</code> |  |
| `password` | <code>string</code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
