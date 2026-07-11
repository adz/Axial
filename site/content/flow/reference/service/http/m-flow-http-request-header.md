---
title: "Flow.Http.Request.header"
linkTitle: "header"
weight: 2303
type: docs
---

 Appends one request header.
 <example><code>request |&gt; Request.header "Accept" "application/json"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Request.header&#32;<span>name&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>string</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
