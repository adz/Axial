---
title: "Flow.Http.Request.query"
linkTitle: "query"
weight: 2301
---

 Appends one URL-encoded query parameter.
 <example><code>request |&gt; Request.query "page" 2</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Request.query&#32;<span>name&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>obj</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
