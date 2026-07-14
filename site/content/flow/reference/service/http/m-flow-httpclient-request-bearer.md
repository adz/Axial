---
title: "Flow.HttpClient.Request.bearer"
linkTitle: "bearer"
weight: 2305
type: docs
---

 Adds a bearer-token Authorization header. The token is always redacted in diagnostics.
 <example><code>request |&gt; Request.bearer token</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.bearer&#32;<span>token&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `token` | <code>string</code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
