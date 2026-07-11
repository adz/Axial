---
title: "Flow.Http.Response.tryHeader"
linkTitle: "tryHeader"
weight: 2403
type: docs
---

 Finds the first header with the given case-insensitive name.
 <example><code>response |&gt; Response.tryHeader "ETag"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Response.tryHeader&#32;<span>name&#32;response</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `response` | <code><a href="t-flow-http-httpresponse.md">HttpResponse</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;option</span></code> |  |
