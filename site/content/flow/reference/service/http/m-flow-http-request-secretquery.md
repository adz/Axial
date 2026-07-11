---
title: "Flow.Http.Request.secretQuery"
linkTitle: "secretQuery"
weight: 2302
type: docs
---

 Appends a query parameter whose value is replaced with <c>***</c> in plans and error transcripts.
 <example><code>request |&gt; Request.secretQuery "api_key" key</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Request.secretQuery&#32;<span>name&#32;value&#32;request</span></span></code>
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
