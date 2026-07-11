---
title: "Flow.Http.DSL.header"
linkTitle: "header"
weight: 2609
---

 Appends one request header. <example><code>request |&gt; header "Accept" "text/csv"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.header&#32;<span>name&#32;value&#32;request</span></span></code>
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
