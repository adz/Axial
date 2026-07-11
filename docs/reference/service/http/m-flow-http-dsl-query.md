---
title: "Flow.Http.DSL.query"
linkTitle: "query"
weight: 2607
---

 Appends a URL-encoded query parameter. <example><code>GET $"{root}/search" |&gt; query "q" term</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.query&#32;<span>name&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>'a</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
