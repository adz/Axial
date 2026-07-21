---
title: "Flow.HttpClient.Request.formBody"
linkTitle: "formBody"
weight: 2315
---

 Sends URL-encoded form fields. <example><code>request |&gt; Request.formBody [ "q", "axial" ]</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.formBody&#32;<span>fields&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fields` | <code><span><span>(<span>string&#32;*&#32;string</span>)</span>&#32;list</span></code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
