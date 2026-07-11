---
title: "Flow.Http.DSL.basicAuth"
linkTitle: "basicAuth"
weight: 2611
---

 Adds a redacted basic-auth Authorization header. <example><code>request |&gt; basicAuth user password</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.basicAuth&#32;<span>user&#32;password&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `user` | <code>string</code> |  |
| `password` | <code>string</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
